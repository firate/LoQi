using LoQi.Application.Common;
using LoQi.Application.DTOs;
using LoQi.Domain;

namespace LoQi.Application.Services;

public interface ILogService
{
    Task<bool> AddLogAsync(AddLogDto dto);
    Task<bool> AddLogsBatchAsync(IReadOnlyList<LogEntry> logEntries);
    Task<PaginatedData<LogDto>>  SearchLogsAsync(LogSearchDto dto);
}