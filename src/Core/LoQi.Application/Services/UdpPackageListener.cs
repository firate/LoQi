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

       
        var channelOptions = new BoundedChannelOptions(5000)
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
            _isRunning = true;

            _logger.LogInformation("UDP listener started on port {Port} with bounded channel capacity {Capacity}", port,
                channelOptions.Capacity);
            
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
        // var messageCount = 0;
        // var lastStatsTime = DateTime.UtcNow;

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
                        _logger.LogWarning("UDP channel is full, dropping oldest messages");
                    }

                    // ✅ Performance monitoring
                    // messageCount++;
                    // if (DateTime.UtcNow - lastStatsTime > TimeSpan.FromSeconds(30))
                    // {
                    //     //_logger.LogDebug("UDP listener processed {MessageCount} messages in last 30 seconds", messageCount);
                    //     messageCount = 0;
                    //     lastStatsTime = DateTime.UtcNow;
                    // }
                }
                catch (OperationCanceledException e)
                {
                    _logger.LogInformation("UDP listener cancelled");
                    break;
                }
                catch (ObjectDisposedException e)
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

        // ✅ Graceful shutdown
        _writer?.Complete();
        _logger.LogInformation("UDP listener stopped gracefully");
    }

    public async Task StopAsync()
    {
        if (!_isRunning) return;

        _logger.LogInformation("Stopping UDP listener...");

        _isRunning = false;
        _udpClient?.Close();

        // ✅ Listener task'ının tamamlanmasını bekle
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

            // ✅ Synchronous wait with timeout
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