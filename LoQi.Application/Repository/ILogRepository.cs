using LoQi.Domain;
using LoQi.Domain.Enums;

namespace LoQi.Application.Repository;

public interface ILogRepository
{
    Task<List<LogEntry>?> SearchLogs(string searchText, DateTimeOffset beginDate, DateTimeOffset endDate,
        LogLevel logLevel);
    Task<LogEntry?> GetLogByIdAsync(long id);
    Task<bool> AddAsync(LogEntry logEntry);
}