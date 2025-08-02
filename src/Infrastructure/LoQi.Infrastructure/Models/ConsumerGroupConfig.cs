namespace LoQi.Infrastructure.Models;

/// <summary>
/// Consumer group specific configuration
/// </summary>
public class ConsumerGroupConfig
{
    public int BatchSize { get; set; } = 100;
    public int BlockTimeMs { get; set; } = 1000;
    public int MaxRetries { get; set; } = 3;
    public long AckTimeoutMs { get; set; } = 30000;
}