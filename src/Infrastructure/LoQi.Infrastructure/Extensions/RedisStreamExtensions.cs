using LoQi.Infrastructure.Models;
using StackExchange.Redis;

namespace LoQi.Infrastructure.Extensions;

/// <summary>
/// Extension methods for easier Redis Stream operations
/// </summary>
public static class RedisStreamExtensions
{
    /// <summary>
    /// Parse log message from stream entry
    /// </summary>
    public static LogStreamMessage ParseLogMessage(this StreamEntry entry)
    {
        var fields = entry.Values.ToDictionary(kv => kv.Name.ToString(), kv => kv.Value.ToString());
        
        return new LogStreamMessage
        {
            Id = entry.Id,
            OriginalData = fields.GetValueOrDefault("originalData", ""),
            Status = Enum.Parse<LogProcessingStatus>(fields.GetValueOrDefault("status", "failed"), true),
            ParsedData = fields.GetValueOrDefault("parsedData"),
            ErrorInfo = fields.GetValueOrDefault("errorInfo"),
            Timestamp = long.Parse(fields.GetValueOrDefault("timestamp", "0")),
            Attempts = int.Parse(fields.GetValueOrDefault("attempts", "1"))
        };
    }
}