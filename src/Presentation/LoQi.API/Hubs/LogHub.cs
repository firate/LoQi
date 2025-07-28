using System.Collections.Concurrent;
using LoQi.Application.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace LoQi.API.Hubs;

public class LogHub: Hub
{
    private static readonly ConcurrentDictionary<string, bool> _liveLogConnections = new();
    private readonly ILogger<LogHub> _logger;

    public LogHub(ILogger<LogHub> logger)
    {
        _logger = logger;
    }

    //  Client live log sayfasını açtığında çağırır
    public async Task JoinLiveLogsGroup()
    {
        var connectionId = Context.ConnectionId;

        _liveLogConnections.AddOrUpdate(connectionId, true, (key, oldValue) => true);
        await Groups.AddToGroupAsync(connectionId, "LiveLogsGroup");
        
        _logger.LogInformation("Client {ConnectionId} joined live logs. Active listeners: {Count}",
            connectionId, GetActiveLiveLogListeners());
    }

    //  Client live log sayfasından çıktığında çağırır
    public async Task LeaveLiveLogsGroup()
    {
        var connectionId = Context.ConnectionId;

        _liveLogConnections.TryUpdate(connectionId, false, true);
        await Groups.RemoveFromGroupAsync(connectionId, "LiveLogsGroup");
        
        _logger.LogInformation("Client {ConnectionId} left live logs. Active listeners: {Count}",
            connectionId, GetActiveLiveLogListeners());
    }

    // 📊 Active listener sayısını döndür
    public static int GetActiveLiveLogListeners()
    {
        return _liveLogConnections.Values.Count(isListening => isListening);
    }

    // 🔗 Connection events
    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        _liveLogConnections.TryAdd(connectionId, false); // Default: not listening
        
        _logger.LogTrace("Client connected: {ConnectionId}", connectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        _liveLogConnections.TryRemove(connectionId, out _);
        
        _logger.LogTrace("Client disconnected: {ConnectionId}. Active listeners: {Count}",
            connectionId, GetActiveLiveLogListeners());
        
        await base.OnDisconnectedAsync(exception);
    }
}

