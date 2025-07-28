using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace LoQi.Application.Services;

public class UdpPackageListener : IUdpPackageListener
{
    private readonly ILogger<UdpPackageListener> _logger;
    private UdpClient? _udpClient;
    private bool _isRunning;
    private Channel<string>? _channel;
    private ChannelWriter<string>? _writer;
    private Task? _listenerTask;

    public UdpPackageListener(ILogger<UdpPackageListener> logger)
    {
        _logger = logger;
    }

    public bool IsRunning => _isRunning;

    public ChannelReader<string> StartAsync(int port, CancellationToken cancellationToken)
    {
        if (_isRunning)
        {
            throw new InvalidOperationException("UDP listener is already running");
        }

        //  INCREASED CAPACITY for high-throughput scenarios
        var channelOptions = new BoundedChannelOptions(100_000) // 100K capacity (was 5K)
        {
            FullMode = BoundedChannelFullMode.DropOldest, 
            SingleReader = true, 
            SingleWriter = true,
            AllowSynchronousContinuations = false // Deadlock prevention
        };

        _channel = Channel.CreateBounded<string>(channelOptions);
        _writer = _channel.Writer;

        try
        {
            _udpClient = new UdpClient(port);
            
            //  OPTIMIZE UDP SOCKET BUFFERS
            _udpClient.Client.ReceiveBufferSize = 8 * 1024 * 1024; // 8MB receive buffer
            _udpClient.Client.SendBufferSize = 1024 * 1024; // 1MB send buffer
            
            _isRunning = true;

            _logger.LogInformation("UDP listener started on port {Port} with channel capacity {Capacity} and socket buffer {BufferSize}KB", 
                port, channelOptions.Capacity, _udpClient.Client.ReceiveBufferSize / 1024);
            
            _listenerTask = ListenAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start UDP listener on port {Port}", port);
            _writer.Complete(ex);
            throw;
        }

        return _channel.Reader;
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        var messageCount = 0;
        var droppedCount = 0;
        var lastStatsTime = DateTime.UtcNow;

        try
        {
            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_udpClient == null) break;
                    
                    var result = await _udpClient.ReceiveAsync(cancellationToken);
                    var message = Encoding.UTF8.GetString(result.Buffer);
                    
                    if (!_writer!.TryWrite(message.Trim()))
                    {
                        // Channel full - DropOldest policy devreye girer
                        droppedCount++;
                        _logger.LogWarning("UDP channel is full, dropping oldest messages. Total dropped: {DroppedCount}", droppedCount);
                    }

                    messageCount++;

                    //  Performance monitoring every 30 seconds
                    if (DateTime.UtcNow - lastStatsTime > TimeSpan.FromSeconds(30))
                    {
                        var messagesPerSecond = messageCount / 30;
                        _logger.LogInformation("UDP listener stats - Messages/sec: {MessagesPerSec}, Total dropped: {DroppedCount}",
                            messagesPerSecond, droppedCount);
                        
                        messageCount = 0;
                        lastStatsTime = DateTime.UtcNow;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("UDP listener cancelled");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogInformation("UDP client disposed");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error receiving UDP packet, retrying in 1 second...");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UDP listener crashed");
            _writer?.Complete(ex);
            return;
        }

        //  Graceful shutdown
        _writer?.Complete();
        _logger.LogInformation("UDP listener stopped gracefully");
    }

    /// <summary>
    /// Estimate buffer usage percentage for monitoring
    /// </summary>
    private int GetEstimatedBufferUsage()
    {
        // Simple approximation based on TryWrite success
        // In production, you might want to implement a custom channel with metrics
        try
        {
            if (_writer == null) return -1;
            
            // Test if we can write without actually writing
            // This is a rough estimation - not exact
            if (_writer.TryWrite(string.Empty))
            {
                // If we can write empty string, channel has space
                // Try to read it back to avoid polluting the stream
                if (_channel?.Reader.TryRead(out _) == true)
                {
                    return 25; // Channel has good space
                }
            }
            else
            {
                return 100; // Channel is full
            }
                
            return 50; // Somewhere in between
        }
        catch
        {
            return -1; // Unknown
        }
    }

    public async Task StopAsync()
    {
        if (!_isRunning) return;

        _logger.LogInformation("Stopping UDP listener...");

        _isRunning = false;
        _udpClient?.Close();

        //  Listener task'ının tamamlanmasını bekle
        if (_listenerTask != null)
        {
            try
            {
                await _listenerTask.WaitAsync(TimeSpan.FromSeconds(5));
                _logger.LogInformation("UDP listener stopped successfully");
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("UDP listener stop timeout - forcing shutdown");
            }
        }

        _writer?.Complete();
    }

    public void Dispose()
    {
        if (_isRunning)
        {
            _isRunning = false;
            _udpClient?.Close();

            //  Synchronous wait with timeout
            try
            {
                _listenerTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during UDP listener disposal");
            }
        }

        _udpClient?.Dispose();
        _writer?.Complete();

        GC.SuppressFinalize(this);
    }
}