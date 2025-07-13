using LoQi.Application.DTOs;

namespace LoQi.Application.Service;

public interface ILogService
{
    Task<bool> AddLogAsync(AddLogDto dto);
}