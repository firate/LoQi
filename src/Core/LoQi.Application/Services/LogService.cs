using LoQi.Application.Common;
using LoQi.Application.DTOs;
using LoQi.Application.Repository;
using LoQi.Domain;
using LoQi.Application.Extensions;
using Microsoft.Extensions.Logging;

namespace LoQi.Application.Services;

public class LogService : ILogService
{
    private readonly ILogRepository _logRepository;
    private readonly IBackgroundNotificationService _backgroundNotificationService;
    private readonly ILogger<LogService> _logger;

    public LogService(
        ILogRepository logRepository, 
        IBackgroundNotificationService backgroundNotificationService,
        ILogger<LogService> logger)
    {
        _logRepository = logRepository;
        _backgroundNotificationService = backgroundNotificationService;
        _logger = logger;
    }

    public async Task<bool> AddLogAsync(AddLogDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto?.Message))
        {
            return false;
        }
        
        Guid? correlationId = null;
        var correlationIdStr = dto?.CorrelationId;
        if (!string.IsNullOrWhiteSpace(correlationIdStr))
        {
            if (Guid.TryParse(correlationIdStr, out Guid parsedCorrelationId))
            {
                correlationId = parsedCorrelationId;
            }
        }

        var log = new LogEntry()
        {
            UniqueId = Guid.NewGuid(),
            Message = dto?.Message!,
            Source = string.IsNullOrWhiteSpace(dto?.Source) ? "Unknown" : dto.Source,
            LevelId = dto?.Level ?? 2,   // 2, "Information"    // default
            Timestamp = DateTimeOffset.Now,
            OffsetMinutes = DateTimeOffset.Now.TotalOffsetMinutes,
            CorrelationId = correlationId
        };

        try
        {
            var isSaved = await _logRepository.AddAsync(log);

            if (!isSaved)
            {
                _logger.LogWarning("Failed to save log entry to database");
                return false;
            }

            //  Non-blocking notification: Smart channel queuing
            _backgroundNotificationService.QueueNotification(log);

            _logger.LogTrace("Log entry saved and notification queued: {LogId}", log.UniqueId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving log entry: {Message}", dto.Message);
            return false;
        }
    }

    public async Task<bool> AddLogsBatchAsync(IReadOnlyList<LogEntry> logEntries)
    {
        if (logEntries?.Count == 0)
        {
            return true; // Empty batch is successful
        }

        if (logEntries == null)
        {
            _logger.LogWarning("Attempted to add null log entries batch");
            return false;
        }

        try
        {
            // Validate log entries before batch insert
            var validLogEntries = logEntries
                .Where(log => !string.IsNullOrWhiteSpace(log?.Message))
                .ToList();

            if (validLogEntries.Count == 0)
            {
                _logger.LogWarning("No valid log entries found in batch of {TotalCount}", logEntries.Count);
                return false;
            }

            if (validLogEntries.Count != logEntries.Count)
            {
                _logger.LogWarning("Filtered {Invalid} invalid entries from batch of {Total}", 
                    logEntries.Count - validLogEntries.Count, logEntries.Count);
            }

            // Batch insert to database
            var isSaved = await _logRepository.AddBatchAsync(validLogEntries);

            if (!isSaved)
            {
                _logger.LogWarning("Failed to save batch of {Count} log entries to database", validLogEntries.Count);
                return false;
            }

            // Queue notifications for real-time updates
            // Note: Only queue if there are active listeners to avoid unnecessary overhead
            if (_backgroundNotificationService.IsEnabled)
            {
                foreach (var logEntry in validLogEntries)
                {
                    _backgroundNotificationService.QueueNotification(logEntry);
                }
            }

            _logger.LogTrace("Batch of {Count} log entries saved and notifications queued", validLogEntries.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving batch of {Count} log entries", logEntries.Count);
            return false;
        }
    }
    
    // Keep existing search method unchanged
    public async Task<PaginatedData<LogDto>> SearchLogsAsync(LogSearchDto dto)
    {
        if (dto == null)
        {
            return PaginatedData<LogDto>.Empty();
        }

        // UniqueId dolu ise tek kayıt olarak arama - FULL MESSAGE ile
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
                    Message = singleLogRecord.Message, // Full message from GetLogByUniqueAsync
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
            Message = x.Message, // Truncated message from SearchLogsAsync
            Source = x.Source,
            UniqueId = x.UniqueId.ToString()
        }).ToList();

        return PaginatedData<LogDto>.Create(
            mappedLogs ?? [],
            pagedResult?.PaginationInfo ?? new PaginationInfo(1, 10, 0));
    }
}