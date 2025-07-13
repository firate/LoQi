using LoQi.Application.DTOs;
using LoQi.Application.Repository;
using LoQi.Domain;

namespace LoQi.Application.Service;

public class LogService: ILogService
{
    private readonly ILogRepository _logRepository;
    private readonly INotificationService _notificationService;

    public LogService(ILogRepository logRepository, INotificationService notificationService)
    {
        _logRepository = logRepository;
        _notificationService = notificationService;
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

        if (!isSaved) return false;
        
        await _notificationService.SendNotification(log);
            
        return true;

    }
}