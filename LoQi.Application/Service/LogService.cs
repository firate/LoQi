using LoQi.Application.DTOs;
using LoQi.Application.Repository;
using LoQi.Domain;

namespace LoQi.Application.Service;

public class LogService: ILogService
{
    private readonly ILogRepository _logRepository;

    public LogService(ILogRepository logRepository)
    {
        _logRepository = logRepository;
    }

    public async Task<bool> AddLogAsync(AddLogDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto?.Message))
        {
            // TODO: I'm going to use ErrorOr library for this type of situation
            return false;
        }

        if (string.IsNullOrWhiteSpace(dto?.Source))
        {
        }

        var log = new LogEntry()
        {
            UniqueId = Guid.NewGuid(),
            Message = dto?.Message!,
            Source = dto?.Source!,
            LevelId = dto.Level,
            Timestamp = DateTimeOffset.Now,
            OffsetMinutes = DateTimeOffset.Now.TotalOffsetMinutes
        };

        var isSaved = await _logRepository.AddAsync(log);

        if (isSaved)
        {
            return true;
        }
        
        return false;
    }
}