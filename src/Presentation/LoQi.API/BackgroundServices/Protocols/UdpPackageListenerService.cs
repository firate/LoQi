using System.Net.Sockets;
using System.Text;
using LoQi.Infrastructure;
using LoQi.Infrastructure.Models;

namespace LoQi.API.BackgroundServices.Protocols;

public class UdpPackageListenerService(
    ILogger<UdpPackageListenerService> logger,
    IRedisStreamService redisStreamService,
    IConfiguration configuration)
    : BackgroundService
{
    private UdpClient? _udpClient;

    private readonly int _port = configuration.GetValue<int>("UdpListener:Port", 10080);

    private bool IsRunning { get; set; }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _udpClient = new UdpClient(_port);

        //  OPTIMIZE UDP SOCKET BUFFERS
        _udpClient.Client.ReceiveBufferSize = 8 * 1024 * 1024; // 8MB receive buffer
        _udpClient.Client.SendBufferSize = 1024 * 1024; // 1MB send buffer

        IsRunning = true;

        logger.LogInformation("UDP listener started on port {Port} and socket buffer {BufferSize}KB",
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
                        logger.LogInformation("No proper log string, udp package skipped!");
                        continue;
                    }

                    await redisStreamService.AddRawUdpMessageAsync(
                        originalData: message,
                        status: LogProcessingStatus.New);
                    
                }
                catch (OperationCanceledException ex)
                {
                    logger.LogInformation(ex, "UDP listener cancelled");
                    break;
                }
                catch (ObjectDisposedException ex)
                {
                    logger.LogInformation(ex, "UDP client disposed");
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error receiving UDP packet, retrying in 1 second...");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "UDP listener crashed");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!IsRunning) return;

        logger.LogInformation("Stopping UDP listener...");

        IsRunning = false;
        _udpClient?.Close();

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        if (IsRunning)
        {
            IsRunning = false;
            _udpClient?.Close();
        }

        _udpClient?.Dispose();

        GC.SuppressFinalize(this);
    }
}