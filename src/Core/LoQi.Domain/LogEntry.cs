using LoQi.Domain.Enums;

namespace LoQi.Domain;

public class LogEntry
{
    public long Id { get; set; }
    public Guid UniqueId { get; set; } = Guid.NewGuid();
    public Guid? CorrelationId { get; set; }
    public string? RedisStreamId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string Source { get; set; } = string.Empty;
    public LogLevel Level { get; set; }

    public int LevelId
    {
        get => (int)Level;
        set => Level = (LogLevel)value;
    }

    public long TimestampUtc => Timestamp.ToUnixTimeSeconds();
}