using StackExchange.Redis;

namespace LoQi.Infrastructure.Models;

/// <summary>
/// Parsed log message from Redis Stream
/// </summary>
public class LogStreamMessage
{
    public RedisValue Id { get; set; }
    public string OriginalData { get; set; } = string.Empty;
    public LogProcessingStatus Status { get; set; }
    public long Timestamp { get; set; }
    public string? ParsedData { get; set; }
    public string? ErrorInfo { get; set; }
    public int Attempts { get; set; }
}