using LoQi.Application.DTOs;

namespace LoQi.Application.Services;

public interface ILogService
{
    Task<bool> AddLogAsync(AddLogDto dto);
    Task<List<LogDto>> SearchLogs(LogSearchDto dto);
}