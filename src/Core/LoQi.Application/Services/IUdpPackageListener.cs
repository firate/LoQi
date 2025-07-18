using System.Threading.Channels;

namespace LoQi.Application.Services;

public interface IUdpPackageListener : IDisposable
{
    ChannelReader<string> StartAsync(int port, CancellationToken cancellationToken);
    Task StopAsync();
    bool IsRunning { get; }
}