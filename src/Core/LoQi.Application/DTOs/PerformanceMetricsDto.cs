namespace LoQi.Application.DTOs;

/// <summary>
/// Performance metrics for system monitoring
/// </summary>
public record PerformanceMetricsDto
{
    /// <summary>
    /// Average logs per minute in the last hour
    /// </summary>
    public double LogsPerMinute { get; init; }

    /// <summary>
    /// Peak logs per minute in the last 24 hours
    /// </summary>
    public double PeakLogsPerMinute { get; init; }

    /// <summary>
    /// Database size in MB
    /// </summary>
    public double DatabaseSizeMB { get; init; }

    /// <summary>
    /// Redis stream length
    /// </summary>
    public long RedisStreamLength { get; init; }

    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.Now;
}