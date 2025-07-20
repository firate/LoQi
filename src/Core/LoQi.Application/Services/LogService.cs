using LoQi.Application.DTOs;
using LoQi.Application.Repository;
using LoQi.Domain;

namespace LoQi.Application.Services;

public class LogService : ILogService
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
        
        var log = new LogEntry()
        {
            UniqueId = Guid.NewGuid(),
            Message = dto?.Message!,
            Source = string.IsNullOrWhiteSpace(dto?.Source) ? "Unknown" : dto.Source,
            LevelId = dto.Level,
            Timestamp = DateTimeOffset.Now,
            OffsetMinutes = DateTimeOffset.Now.TotalOffsetMinutes
        };

        var isSaved = await _logRepository.AddAsync(log);

        if (!isSaved) return false;

        await _notificationService.SendNotification(log);

        return true;
    }

    public async Task<List<LogDto>> SearchLogs(LogSearchDto dto)
    {
        // UniqueId dolu ise tek kayıt olarak,
        // doğrudan unique_id'den sorgula

        // pattern'leri al, dönüştür.
        // tarihleri al

        if (!string.IsNullOrWhiteSpace(dto.UniqueId))
        {
            var singleLogRecord = await _logRepository.GetLogByUniqueAsync(dto.UniqueId);

            if (singleLogRecord is null)
            {
                return [];
            }

            return new List<LogDto>()
            {
                new LogDto()
                {
                    CorrelationId = singleLogRecord?.CorrelationId.ToString(),
                    LevelId = singleLogRecord?.LevelId ?? 2,
                    UniqueId = singleLogRecord?.UniqueId != Guid.Empty
                        ? singleLogRecord?.UniqueId.ToString()
                        : string.Empty,
                    Message = singleLogRecord?.Message ?? string.Empty,
                    Source = singleLogRecord?.Source ?? string.Empty,
                    
                }
            };
        }


        var x = await _logRepository.GetLogByUniqueAsync(dto.UniqueId);


        return null;
    }
    

}