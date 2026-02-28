namespace LoQi.Application.DTOs;

/// <summary>
/// Log statistics data transfer object for dashboard
/// </summary>
public record LogStatisticsDto
{
    /// <summary>
    /// Total number of logs received today
    /// </summary>
    public int TotalLogsToday { get; init; }

    /// <summary>
    /// Total number of logs received this week
    /// </summary>
    public int TotalLogsThisWeek { get; init; }

    /// <summary>
    /// Total number of logs received this month
    /// </summary>
    public int TotalLogsThisMonth { get; init; }

    /// <summary>
    /// Number of error level logs (Error + Fatal)
    /// </summary>
    public int ErrorCount { get; init; }

    /// <summary>
    /// Number of warning level logs
    /// </summary>
    public int WarningCount { get; init; }

    /// <summary>
    /// Number of information level logs
    /// </summary>
    public int InfoCount { get; init; }

    /// <summary>
    /// Number of debug level logs
    /// </summary>
    public int DebugCount { get; init; }

    /// <summary>
    /// Top log sources by volume
    /// </summary>
    public Dictionary<string, int> TopSources { get; init; } = new();

    /// <summary>
    /// Log level distribution
    /// </summary>
    public Dictionary<string, int> LogLevelDistribution { get; init; } = new();

    /// <summary>
    /// Hourly log counts for the last 24 hours
    /// </summary>
    public List<HourlyLogCount> HourlyLogCounts { get; init; } = new();

    /// <summary>
    /// Recent error samples for quick review
    /// </summary>
    public List<LogSampleDto> RecentErrors { get; init; } = new();

    /// <summary>
    /// Performance metrics
    /// </summary>
    public PerformanceMetricsDto Performance { get; init; } = new();
}