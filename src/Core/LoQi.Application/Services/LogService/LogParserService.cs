using System.Text.Json;
using LoQi.Application.DTOs;
using LoQi.Domain;
using Microsoft.Extensions.Logging;
using LogLevel = LoQi.Domain.Enums.LogLevel;

namespace LoQi.Application.Services.LogService;

/// <summary>
/// Log parser service implementation with multiple parsing strategies
/// </summary>
public class LogParserService(ILogger<LogParserService> logger) : ILogParserService
{
    /// <summary>
    /// Convert multiple raw log messages to LogEntry objects
    /// </summary>
    public async Task<List<LogEntry>> ConvertToLogEntryAsync(List<RawLogDto> rawMessages)
    {
        if (rawMessages.Count == 0)
        {
            return [];
        }

        var results = new List<LogEntry>();
        var tasks = rawMessages.Select(async msg =>
        {
            try
            {
                return await ConvertToLogEntryAsync(msg);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error parsing message in batch");
                return null;
            }
        });

        var parsedResults = await Task.WhenAll(tasks);

        foreach (var result in parsedResults)
        {
            if (result != null)
            {
                results.Add(result);
            }
        }

        logger.LogDebug("Successfully parsed {SuccessCount} out of {TotalCount} messages",
            results.Count, rawMessages.Count);

        return results;
    }

    /// <summary>
    /// Convert a single raw log DTO to LogEntry with Redis Stream ID preservation
    /// </summary>
    private async Task<LogEntry?> ConvertToLogEntryAsync(RawLogDto rawLogDto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rawLogDto.OriginalData))
            {
                logger.LogDebug("Empty raw log data, skipping. StreamId: {StreamId}", rawLogDto.RedisStreamId);
                return null;
            }

            // Validate message quality
            if (!IsValidLogMessage(rawLogDto.OriginalData))
            {
                logger.LogDebug("Invalid log message format, skipping. StreamId: {StreamId}, Message: {Message}",
                    rawLogDto.RedisStreamId,
                    rawLogDto.OriginalData[..Math.Min(100, rawLogDto.OriginalData.Length)]);
                return null;
            }

            // Parse the raw message using existing logic
            var logEntry = await Task.Run(() => ParseRawMessage(rawLogDto.OriginalData));

            if (logEntry == null)
            {
                logger.LogDebug("Failed to parse raw message. StreamId: {StreamId}, Message: {Message}",
                    rawLogDto.RedisStreamId,
                    rawLogDto.OriginalData[..Math.Min(100, rawLogDto.OriginalData.Length)]);
                return null;
            }

            // Preserve Redis Stream ID for traceability
            logEntry.RedisStreamId = rawLogDto.RedisStreamId;

            logger.LogTrace(
                "Successfully parsed log entry. StreamId: {StreamId}, UniqueId: {UniqueId}, Level: {Level}, Timestamp: {Timestamp}",
                logEntry.RedisStreamId, logEntry.UniqueId, logEntry.LevelId, logEntry.Timestamp);

            return logEntry;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error converting raw log DTO to LogEntry. StreamId: {StreamId}",
                rawLogDto.RedisStreamId);
            return null;
        }
    }

    #region Private Helper Methods

    private LogEntry? ParseRawMessage(string rawMessage)
    {
        return TryParseAsJsonSync(rawMessage) ??
               TryParseAsPlainTextSync(rawMessage);
    }

    private LogEntry? TryParseAsJsonSync(string rawMessage)
    {
        try
        {
            if (!rawMessage.TrimStart().StartsWith('{') || !rawMessage.TrimEnd().EndsWith('}'))
            {
                return null;
            }

            var jsonDoc = JsonDocument.Parse(rawMessage);
            var root = jsonDoc.RootElement;

            var message = GetJsonProperty(root, "message", "msg", "text", "@message") ?? rawMessage;
            var level = GetJsonProperty(root, "level", "severity", "loglevel", "@level") ?? "Information";
            var source = GetJsonProperty(root, "source", "logger", "category", "@source") ?? "JSON";
            var correlationId = GetJsonProperty(root, "correlationId", "correlation_id", "traceId", "trace_id");

            // Parse level
            var logLevel = ParseLogLevel(level);

            // Parse correlation ID
            Guid? correlationGuid = null;
            if (!string.IsNullOrWhiteSpace(correlationId) && Guid.TryParse(correlationId, out var guid))
            {
                correlationGuid = guid;
            }

            // SERVER-SIDE UTC TIMESTAMP for security and consistency
            var utcTimestamp = DateTimeOffset.UtcNow;

            return new LogEntry
            {
                UniqueId = Guid.NewGuid(),
                CorrelationId = correlationGuid,
                Message = message,
                Source = source,
                LevelId = (int)logLevel,
                Timestamp = utcTimestamp
            };
        }
        catch (JsonException)
        {
            return null; // Not valid JSON
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error parsing JSON log message");
            return null;
        }
    }

    private LogEntry? TryParseAsPlainTextSync(string rawMessage)
    {
        try
        {
            var serverTimestamp = DateTimeOffset.UtcNow;

            return new LogEntry
            {
                UniqueId = Guid.NewGuid(),
                Message = rawMessage.Trim(),
                Source = "PlainText",
                LevelId = (int)LogLevel.Information, // Default level
                Timestamp = serverTimestamp
            };
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error creating plain text log entry");
            return null;
        }
    }

    private static string? GetJsonProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var property))
            {
                return property.GetString();
            }
        }

        return null;
    }

    private static LogLevel ParseLogLevel(string? levelStr)
    {
        if (string.IsNullOrWhiteSpace(levelStr))
        {
            return LogLevel.Information;
        }

        var level = levelStr.Trim().ToUpperInvariant();

        return level switch
        {
            "VERBOSE" or "TRACE" or "ALL" => LogLevel.Verbose,
            "DEBUG" or "DBG" => LogLevel.Debug,
            "INFO" or "INFORMATION" or "INF" => LogLevel.Information,
            "WARN" or "WARNING" or "WRN" => LogLevel.Warning,
            "ERROR" or "ERR" or "EXCEPTION" => LogLevel.Error,
            "FATAL" or "CRITICAL" or "CRIT" => LogLevel.Fatal,
            _ => LogLevel.Information
        };
    }
    
    private static bool IsValidLogMessage(string rawMessage)
    {
        var trimmed = rawMessage.Trim();
        return trimmed.Length >= 1 && trimmed.Length <= 50000;
    }


    #endregion
}