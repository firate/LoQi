using LoQi.API.Hubs;
using LoQi.Application.Services;
using Microsoft.AspNetCore.SignalR;

namespace LoQi.API.Services;

public class SignalRNotification: INotificationService
{
    private readonly IHubContext<LogHub> _hubContext;

    public SignalRNotification(IHubContext<LogHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendNotification(object data)
    {
        await _hubContext.Clients.All.SendAsync("NewLogEntry", data);
    }
}