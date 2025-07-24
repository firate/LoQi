using LoQi.Application.Common;
using LoQi.Application.DTOs;

namespace LoQi.Application.Services;

public interface ILogService
{
    Task<bool> AddLogAsync(AddLogDto dto);
    Task<PaginatedData<LogDto>>  SearchLogsAsync(LogSearchDto dto);
}