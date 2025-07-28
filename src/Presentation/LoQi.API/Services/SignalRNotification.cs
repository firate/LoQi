// LoQi.API/Services/SignalRNotification.cs - Bu kısmı GÜNCELLE

using LoQi.Application.Services;
using LoQi.API.Hubs;
using Microsoft.AspNetCore.SignalR;
using LoQi.Domain;

namespace LoQi.API.Services;

public class SignalRNotification : INotificationService
{
    private readonly IHubContext<LogHub> _hubContext;
    private readonly ILogger<SignalRNotification> _logger;

    public SignalRNotification(
        IHubContext<LogHub> hubContext,
        ILogger<SignalRNotification> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendNotification(object data)
    {
        try
        {
            //  LogEntry'yi frontend formatına çevir
            if (data is LogEntry logEntry)
            {
                var logDto = new
                {
                    id = logEntry.Id,
                    message = logEntry.Message,
                    level = logEntry.LevelId,
                    source = logEntry.Source,
                    timestamp = logEntry.Timestamp.ToString("o"), // ISO format
                    correlationId = logEntry.CorrelationId?.ToString()
                };

                await _hubContext.Clients.Group("LiveLogsGroup").SendAsync("NewLogEntry", logDto);
                
                _logger.LogTrace("SignalR notification sent to LiveLogsGroup for log {LogId}", logEntry.UniqueId);
            }
            else
            {
                // Fallback: raw data gönder
                await _hubContext.Clients.Group("LiveLogsGroup").SendAsync("NewLogEntry", data);
                
                _logger.LogTrace("SignalR notification sent to LiveLogsGroup (raw data)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR notification failed");
            throw; // Re-throw to let background service handle the error
        }
    }
}