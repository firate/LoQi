using LoQi.Domain.Enums;

namespace LoQi.Domain;

public class LogEntry
{
    public Guid Id { get; set; }
    public string LogMessage { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public LogLevel LogLevel { get; set; }

    public int LogLevelId
    {
        get => (int)LogLevel;
        set => LogLevel = (LogLevel)value;
    }
}