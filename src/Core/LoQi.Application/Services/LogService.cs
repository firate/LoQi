using LoQi.Application.Common;
using LoQi.Application.DTOs;
using LoQi.Application.Repository;
using LoQi.Domain;
using LoQi.Application.Extensions;

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

    public async Task<PaginatedData<LogDto>> SearchLogsAsync(LogSearchDto dto)
    {
        if (dto == null)
        {
            return PaginatedData<LogDto>.Empty();
        }

        // UniqueId dolu ise tek kayıt olarak arama
        if (!string.IsNullOrWhiteSpace(dto.UniqueId))
        {
            var singleLogRecord = await _logRepository.GetLogByUniqueAsync(dto.UniqueId);
            if (singleLogRecord is null)
            {
                return PaginatedData<LogDto>.Empty();
            }

            var oneItemList = new List<LogDto>()
            {
                new()
                {
                    CorrelationId = singleLogRecord.CorrelationId?.ToString(),
                    LevelId = singleLogRecord.LevelId,
                    UniqueId = singleLogRecord.UniqueId.ToString(),
                    Message = singleLogRecord.Message,
                    Source = singleLogRecord.Source,
                    Date = singleLogRecord.Timestamp
                }
            };

            return PaginatedData<LogDto>.Create(oneItemList, 1, 1, 1);
        }

        // Tarih değerlerini güvenli şekilde parse et
        var startDate = dto.StartDate.ParseDateTimeOffset() ?? DateTimeOffset.UtcNow.AddDays(-7);
        var endDate = dto.EndDate.ParseDateTimeOffset() ?? DateTimeOffset.UtcNow;

        var pagedResult = await _logRepository.SearchLogsAsync(
            searchText: !string.IsNullOrWhiteSpace(dto?.SearchText) ? dto?.SearchText : string.Empty,
            startDate: startDate,
            endDate: endDate,
            levelId: dto?.LevelId,
            source: dto?.Source,
            correlationId: dto?.CorrelationId,
            page: dto?.Page ?? 1,
            orderBy: string.IsNullOrWhiteSpace(dto?.OrderBy) ? "timestamp" : dto?.OrderBy,
            pageSize: dto?.PageSize ?? 50,
            descending: dto?.Descending ?? true
        );

        if (pagedResult?.Items?.Count <= 0)
        {
            return PaginatedData<LogDto>.Empty();
        }

        var mappedLogs = pagedResult?.Items?.Select(x => new LogDto()
        {
            CorrelationId = x?.CorrelationId?.ToString(),
            Date = x.Timestamp,
            LevelId = x.LevelId,
            Message = x.Message,
            Source = x.Source,
            UniqueId = x.UniqueId.ToString()
        }).ToList();

        return PaginatedData<LogDto>.Create(
            mappedLogs ?? [],
            pagedResult?.PaginationInfo ?? new PaginationInfo(1,10, 0));
    }
    
}