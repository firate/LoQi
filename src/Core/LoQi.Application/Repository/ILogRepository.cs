using LoQi.Application.Common;
using LoQi.Application.DTOs;
using LoQi.Domain;

namespace LoQi.Application.Repository;

public interface ILogRepository
{
 


    Task<LogEntry?> GetLogByUniqueAsync(string uniqueId);
    Task<bool> AddAsync(LogEntry logEntry);


    // Task<LogSearchResult> SearchLogsAsync(
    //     string searchText,
    //     DateTimeOffset startDate,
    //     DateTimeOffset endDate,
    //     int levelId,
    //     string? source,
    //     string? correlationId,
    //     int page,
    //     string orderBy,
    //     int pageSize = 50,
    //     bool descending = true
    // );

    Task<PagedResult<LogEntry>> SearchLogsAsync(string searchText, DateTimeOffset startDate, DateTimeOffset endDate,
        int levelId, string? source,
        string? correlationId, int page, string orderBy, int pageSize = 50, bool descending = true);
    
    // Task<PagedResult<LogDto>> SearchLogsAsync(string searchText, DateTimeOffset startDate, DateTimeOffset endDate,
    //     int levelId, string? source,
    //     string? correlationId, int page, string orderBy, int pageSize = 50, bool descending = true);

}