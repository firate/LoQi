using LoQi.Application.DTOs;
using LoQi.Domain;

namespace LoQi.Application.Services.LogService;

/// <summary>
/// Service for parsing raw log messages into structured LogEntry objects
/// Uses server-side UTC timestamps for security and consistency
/// </summary>
public interface ILogParserService
{
    /// <summary>
    /// Convert multiple raw log messages to LogEntry objects
    /// </summary>
    /// <param name="rawMessages">List of raw log message strings</param>
    /// <returns>List of successfully parsed LogEntry objects</returns>
    Task<List<LogEntry>> ConvertToLogEntryAsync(List<RawLogDto> rawMessages);
    
}