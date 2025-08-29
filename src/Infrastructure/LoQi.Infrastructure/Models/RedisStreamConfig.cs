namespace LoQi.Infrastructure.Models;

/// <summary>
/// Redis Stream configuration
/// </summary>
public class RedisStreamConfig
{
    public string StreamName { get; set; } = "loqi:logs:stream";
    public long MaxStreamLength { get; set; } = 1_000_000;
    public int DefaultBatchSize { get; set; } = 100;
    public int DefaultBlockTimeMs { get; set; } = 1000;
    public long MessageRetentionMs { get; set; } = 24 * 60 * 60 * 1000; // 24 hours
    
    /// <summary>
    /// Consumer group configurations
    /// </summary>
    public Dictionary<string, ConsumerGroupConfig> ConsumerGroups { get; set; } = new()
    {
        ["raw-logs"] = new() { BatchSize = 500, BlockTimeMs = 1000 },
        ["failed-logs"] = new() { BatchSize = 50, BlockTimeMs = 5000 },
        ["retry-logs"] = new() { BatchSize = 100, BlockTimeMs = 2000 }
    };
}