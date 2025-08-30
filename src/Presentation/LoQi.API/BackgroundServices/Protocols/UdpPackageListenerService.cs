using System.Net.Sockets;
using System.Text;
using LoQi.Infrastructure;
using LoQi.Infrastructure.Models;

namespace LoQi.API.BackgroundServices.Protocols;

public class UdpPackageListenerService : BackgroundService
{
    private readonly ILogger<UdpPackageListenerService> _logger;
    private UdpClient? _udpClient;
    private bool _isRunning;

    private readonly IConfiguration _configuration;
    private readonly IRedisStreamService _redisStreamService;
    private readonly int _port;

    public UdpPackageListenerService(
        ILogger<UdpPackageListenerService> logger,
        IRedisStreamService redisStreamService,
        IConfiguration configuration)
    {
        _logger = logger;
        _redisStreamService = redisStreamService;
        _configuration = configuration;
        _port = _configuration.GetValue<int>("UdpListener:Port", 10080);
    }

    private bool IsRunning => _isRunning;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _udpClient = new UdpClient(_port);

        //  OPTIMIZE UDP SOCKET BUFFERS
        _udpClient.Client.ReceiveBufferSize = 8 * 1024 * 1024; // 8MB receive buffer
        _udpClient.Client.SendBufferSize = 1024 * 1024; // 1MB send buffer

        _isRunning = true;

        _logger.LogInformation("UDP listener started on port {Port} and socket buffer {BufferSize}KB",
            _port,
            _udpClient.Client.ReceiveBufferSize / 1024);

        try
        {
            while (IsRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_udpClient == null)
                    {
                        break;
                    }

                    var result = await _udpClient.ReceiveAsync(cancellationToken);
                    var message = Encoding.UTF8.GetString(result.Buffer);

                    // if there is no string iterate to next udp message
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        _logger.LogInformation("No proper log string, udp package skipped!");
                        continue;
                    }

                    await _redisStreamService.AddRawUdpMessageAsync(
                        originalData: message,
                        status: LogProcessingStatus.New);
                    
                }
                catch (OperationCanceledException ex)
                {
                    _logger.LogInformation(ex, "UDP listener cancelled");
                    break;
                }
                catch (ObjectDisposedException ex)
                {
                    _logger.LogInformation(ex, "UDP client disposed");
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
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!IsRunning) return;

        _logger.LogInformation("Stopping UDP listener...");

        _isRunning = false;
        _udpClient?.Close();

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        if (IsRunning)
        {
            _isRunning = false;
            _udpClient?.Close();
        }

        _udpClient?.Dispose();

        GC.SuppressFinalize(this);
    }
}