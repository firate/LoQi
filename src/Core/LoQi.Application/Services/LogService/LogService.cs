using LoQi.Application.Common;
using LoQi.Application.DTOs;
using LoQi.Application.Extensions;
using LoQi.Application.Repository;
using LoQi.Domain;
using Microsoft.Extensions.Logging;
using LogLevel = LoQi.Domain.Enums.LogLevel;

namespace LoQi.Application.Services.LogService;

/// <summary>
/// Log service implementation with repository pattern, DTO mapping, and business logic
/// All timestamps are stored as UTC for consistency across timezones
/// </summary>
public class LogService(ILogRepository logRepository, ILogger<LogService> logger) : ILogService
{
    public async Task<bool> AddLogsBatchAsync(IReadOnlyList<LogEntry> logEntries)
    {
        try
        {
            if (logEntries.Count == 0)
            {
                logger.LogInformation("AddLogsBatchAsync called with empty or null list");
                return true; // Empty batch is considered successful
            }

            // Validate entries
            var validEntries = logEntries.Where(ValidateLogEntry).ToList();

            if (validEntries.Count != logEntries.Count)
            {
                logger.LogWarning("Filtered out {InvalidCount} invalid entries from batch of {TotalCount}",
                    logEntries.Count - validEntries.Count, logEntries.Count);
            }

            if (validEntries.Count == 0)
            {
                logger.LogWarning("No valid entries found in batch of {TotalCount}", logEntries.Count);
                return false;
            }

            // Add batch to repository
            var isAdded = await logRepository.AddBatchAsync(validEntries);

            if (isAdded)
            {
                logger.LogInformation("Successfully added batch of {Count} log entries", validEntries.Count);
            }
            else
            {
                logger.LogError("Failed to add batch of {Count} log entries", validEntries.Count);
            }

            return isAdded;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding batch of {Count} log entries", logEntries?.Count ?? 0);
            return false;
        }
    }

    /// <summary>
    /// Add multiple logs using DTOs in batch
    /// </summary>
    public async Task<bool> AddLogsBatchAsync(IReadOnlyList<AddLogDto> addLogDtos)
    {
        try
        {
            if (addLogDtos.Count == 0)
            {
                return true; // Empty batch is successful
            }

            // Convert DTOs to entities
            var logEntries = addLogDtos
                .Where(dto => !string.IsNullOrWhiteSpace(dto?.Message))
                .Select(MapToEntity)
                .ToList();

            return await AddLogsBatchAsync(logEntries);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error converting and adding batch of {Count} DTOs", addLogDtos?.Count ?? 0);
            return false;
        }
    }

    /// <summary>
    /// Search logs with filtering, pagination and sorting
    /// </summary>
    public async Task<PaginatedData<LogDto>?> SearchLogsAsync(LogSearchDto searchDto)
    {
        try
        {
            // // Input validation and defaults
            // if (searchDto is null)
            // {
            //     logger.LogWarning("SearchLogsAsync called with null SearchDto");
            //     return PaginatedData<LogDto>.Empty();
            // }

            // Handle UniqueId search (direct lookup)
            if (!string.IsNullOrWhiteSpace(searchDto.UniqueId))
            {
                return await SearchByUniqueIdAsync(searchDto.UniqueId);
            }

            // Parse and validate dates (UTC)
            var startDate =
                ParseDate(searchDto?.StartDate, DateTimeOffset.UtcNow.AddDays(-7), searchDto!.OffsetInMinutes); // Default: 7 days ago UTC
            var endDate = ParseDate(searchDto?.EndDate, DateTimeOffset.UtcNow, searchDto!.OffsetInMinutes); // Default: now UTC

            // Validate date range
            if (startDate > endDate)
            {
                logger.LogWarning("Invalid date range: StartDate {StartDate} > EndDate {EndDate}",
                    startDate, endDate);
                return PaginatedData<LogDto>.Empty();
            }

            // Search with repository
            var result = await logRepository.SearchLogsAsync(
                searchText: searchDto.SearchText?.Trim(),
                startDate: startDate,
                endDate: endDate,
                levelId: searchDto.LevelId,
                source: searchDto.Source?.Trim(),
                correlationId: searchDto.CorrelationId?.Trim(),
                page: Math.Max(1, searchDto.Page),
                orderBy: searchDto.OrderBy?.Trim(),
                offSetInMinutes: searchDto.OffsetInMinutes,
                pageSize: Math.Clamp(searchDto.PageSize, 1, 100),
                descending: searchDto.Descending
            );

            if (result?.Items == null || result.Items.Count == 0)
            {
                logger.LogDebug("No logs found for search criteria");
                return PaginatedData<LogDto>.Empty(searchDto.Page, searchDto.PageSize);
            }

            // Map entities to DTOs
            var logDtos = result.Items.Select(MapToDto).ToList();

            var paginatedResult = new PaginatedData<LogDto>(logDtos, result.PaginationInfo);

            logger.LogDebug("Found {Count} logs for search, total: {Total}",
                logDtos.Count, result.PaginationInfo.TotalCount);

            return paginatedResult;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching logs");
            return PaginatedData<LogDto>.Empty();
        }
    }

    /// <summary>
    /// Get a specific log by its unique identifier
    /// </summary>
    public async Task<LogDto?> GetLogByUniqueIdAsync(string uniqueId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(uniqueId))
            {
                logger.LogWarning("GetLogByUniqueIdAsync called with empty uniqueId");
                return null;
            }

            if (!Guid.TryParse(uniqueId, out _))
            {
                logger.LogWarning("GetLogByUniqueIdAsync called with invalid GUID format: {UniqueId}", uniqueId);
                return null;
            }

            var logEntry = await logRepository.GetLogByUniqueAsync(uniqueId);

            if (logEntry is not null)
            {
                return MapToDto(logEntry);
            }
            
            logger.LogDebug("Log not found for UniqueId: {UniqueId}", uniqueId);
            return null;

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting log by UniqueId: {UniqueId}", uniqueId);
            return null;
        }
    }

    /// <summary>
    /// Get log statistics for dashboard
    /// </summary>
    public async Task<LogStatisticsDto> GetLogStatisticsAsync()
    {
        try
        {
            // Implementation would require additional repository methods
            // For now, return basic stats based on recent logs search
            var recentLogs = await GetRecentLogsAsync(1000);
            var utcNow = DateTimeOffset.UtcNow;

            var stats = new LogStatisticsDto
            {
                TotalLogsToday = recentLogs.Count(l => l.Date.Date == utcNow.Date),
                TotalLogsThisWeek = recentLogs.Count(l => l.Date >= utcNow.AddDays(-7)),
                ErrorCount = recentLogs.Count(l => l.LevelId >= (int)LogLevel.Error),
                WarningCount = recentLogs.Count(l => l.LevelId == (int)LogLevel.Warning),
                TopSources = recentLogs
                    .GroupBy(l => l.Source)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            return stats;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting log statistics");
            return new LogStatisticsDto(); // Return empty stats
        }
    }

    /// <summary>
    /// Get recent logs for real-time monitoring
    /// </summary>
    public async Task<List<LogDto>> GetRecentLogsAsync(int count = 100, int? minLevel = null)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var searchDto = new LogSearchDto
            {
                StartDate = now.AddHours(-24).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), // Last 24 hours UTC
                EndDate = now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), // Current time UTC
                LevelId = minLevel,
                Page = 1,
                PageSize = Math.Clamp(count, 1, 1000),
                OrderBy = "timestamp",
                Descending = true
            };

            var result = await SearchLogsAsync(searchDto);
            return result?.Items ?? new List<LogDto>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting recent logs");
            return new List<LogDto>();
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Search by unique ID (special case for single log lookup)
    /// </summary>
    private async Task<PaginatedData<LogDto>> SearchByUniqueIdAsync(string uniqueId)
    {
        var log = await GetLogByUniqueIdAsync(uniqueId);

        if (log == null)
        {
            return PaginatedData<LogDto>.Empty();
        }

        var logs = new List<LogDto> { log };
        var pagination = new PaginationInfo(1, 1, 1);

        return new PaginatedData<LogDto>(logs, pagination);
    }

    private static DateTimeOffset ParseDate(string? dateString, DateTimeOffset fallback, int offsetInMinutes = 0)
    {
        if (string.IsNullOrWhiteSpace(dateString)) return fallback.AddMinutes(offsetInMinutes);
        
        var parsed = dateString.ParseDateTimeOffset();
        return parsed?.AddMinutes(offsetInMinutes) ?? fallback;
    }

    /// <summary>
    /// Map AddLogDto to LogEntry entity with server-side UTC timestamp
    /// </summary>
    private static LogEntry MapToEntity(AddLogDto dto)
    {
        var correlationId = string.IsNullOrWhiteSpace(dto.CorrelationId)
            ? null
            : Guid.TryParse(dto.CorrelationId, out var guid)
                ? guid
                : (Guid?)null;

        // SERVER-SIDE UTC TIMESTAMP for security and consistency
        var utcTimestamp = DateTimeOffset.UtcNow;

        return new LogEntry
        {
            UniqueId = Guid.NewGuid(),
            CorrelationId = correlationId,
            Message = dto.Message?.Trim() ?? string.Empty,
            Source = dto.Source?.Trim() ?? "Unknown",
            LevelId = Math.Clamp(dto.LevelId, 0, 5), // Ensure valid level
            Timestamp = utcTimestamp,
            RedisStreamId = dto?.RedisStreamId
        };
    }

    /// <summary>
    /// Map LogEntry entity to LogDto
    /// </summary>
    private static LogDto MapToDto(LogEntry entity)
    {
        return new LogDto
        {
            UniqueId = entity.UniqueId.ToString(),
            CorrelationId = entity.CorrelationId?.ToString(),
            RedisStreamId = entity.RedisStreamId,
            Message = entity.Message,
            Source = entity.Source,
            LevelId = entity.LevelId,
            Date = entity.Timestamp
        };
    }

    /// <summary>
    /// Validate log entry before adding
    /// </summary>
    private static bool ValidateLogEntry(LogEntry? entry)
    {
        return entry != null &&
               !string.IsNullOrWhiteSpace(entry.Message) &&
               !string.IsNullOrWhiteSpace(entry.Source) &&
               entry.LevelId is >= 0 and <= 5;
    }

    #endregion
}