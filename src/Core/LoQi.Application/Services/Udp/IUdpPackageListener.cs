using System.Threading.Channels;

namespace LoQi.Application.Services.Udp;

public interface IUdpPackageListener : IDisposable
{
    ChannelReader<string> StartAsync(int port, CancellationToken cancellationToken);
    Task StopAsync();
    bool IsRunning { get; }
}