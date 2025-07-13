using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LoQi.Application.DTOs;
using LoQi.Application.Service;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace LoQi.API.BackgroundServices;

public class UdpListenerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private UdpClient _udpClient;

    public UdpListenerService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _udpClient = new UdpClient(8080);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync();
                var message = Encoding.UTF8.GetString(result.Buffer);
                
                await ProcessUdpMessage(message, result.RemoteEndPoint);
            }
            catch (Exception e)
            {
                // TODO: log this
            }
        }
    }

    private async Task ProcessUdpMessage(string message, IPEndPoint remoteEndPoint)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var logService = scope.ServiceProvider.GetRequiredService<ILogService>();

        var dto = JsonSerializer.Deserialize<AddLogDto>(message, new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });

        if (dto is null)
        {
            // TODO: log this
            return;
        }
        
        await logService.AddLogAsync(dto);
    }

    public override void Dispose()
    {
        _udpClient?.Close();
        _udpClient?.Dispose();
        base.Dispose();
    }
}