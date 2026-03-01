using LoQi.Application.Common;
using LoQi.Application.DTOs;
using LoQi.Domain;

namespace LoQi.Application.Services.LogService;

/// <summary>
/// Log service interface for managing log entries
/// </summary>
public interface ILogService
{
    /// <summary>
    /// Add multiple log entries in batch (high performance)
    /// </summary>
    /// <param name="logEntries">List of log entries to add</param>
    /// <returns>True if all logs were added successfully</returns>
    Task<bool> AddLogsBatchAsync(IReadOnlyList<LogEntry> logEntries);

    /// <summary>
    /// Add multiple logs using DTOs in batch
    /// </summary>
    /// <param name="addLogDtos">List of log DTOs to add</param>
    /// <returns>True if all logs were added successfully</returns>
    Task<bool> AddLogsBatchAsync(IReadOnlyList<AddLogDto> addLogDtos);

    /// <summary>
    /// Search logs with filtering, pagination and sorting
    /// </summary>
    /// <param name="searchDto">Search criteria and pagination parameters</param>
    /// <returns>Paginated search results</returns>
    Task<PaginatedData<LogDto>?> SearchLogsAsync(LogSearchDto searchDto);

    /// <summary>
    /// Get a specific log by its unique identifier
    /// </summary>
    /// <param name="uniqueId">Unique identifier of the log</param>
    /// <returns>Log details or null if not found</returns>
    Task<LogDto?> GetLogByUniqueIdAsync(string uniqueId);

    /// <summary>
    /// Get log statistics for dashboard
    /// </summary>
    /// <returns>Log statistics</returns>
    Task<LogStatisticsDto> GetLogStatisticsAsync();

    /// <summary>
    /// Get recent logs for real-time monitoring
    /// </summary>
    /// <param name="count">Number of recent logs to retrieve</param>
    /// <param name="minLevel">Minimum log level to include</param>
    /// <returns>Recent log entries</returns>
    Task<List<LogDto>> GetRecentLogsAsync(int count = 100, int? minLevel = null);
}