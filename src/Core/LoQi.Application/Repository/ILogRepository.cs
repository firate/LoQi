using LoQi.Application.Common;
using LoQi.Domain;

namespace LoQi.Application.Repository;

public interface ILogRepository
{
    Task<LogEntry?> GetLogByUniqueAsync(string uniqueId);
    Task<bool> AddAsync(LogEntry logEntry);
    Task<bool> AddBatchAsync(IReadOnlyList<LogEntry> logEntries);
    Task<PaginatedData<LogEntry>> SearchLogsAsync(string? searchText, DateTimeOffset startDate, DateTimeOffset endDate,
        int? levelId, string? source,
        string? correlationId, int page, string? orderBy, int pageSize = 50, bool descending = true);
    
}