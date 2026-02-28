namespace LoQi.Application.DTOs;

/// <summary>
/// Hourly log count for trending charts
/// </summary>
public record HourlyLogCount
{
    public DateTimeOffset Hour { get; init; }
    public int Count { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
}