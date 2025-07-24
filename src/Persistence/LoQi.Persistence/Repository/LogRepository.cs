using Dapper;
using LoQi.Application.Common;
using LoQi.Application.Repository;
using LoQi.Domain;

namespace LoQi.Persistence.Repository;

public class LogRepository : ILogRepository
{
    private readonly DataContext _context;

    public LogRepository(DataContext context)
    {
        _context = context;
    }

    public async Task<LogEntry?> GetLogByUniqueAsync(string uniqueId)
    {
        var sqlQuery = @"
            SELECT id, 
                   unique_id, 
                   correlation_id, 
                   message,
                   timestamp, 
                   offset_minutes, 
                   source, 
                   level 
            FROM logs 
            WHERE UPPER(unique_id) = UPPER(@uniqueId)";

        var connection = _context.CreateConnection();

        var rawResult = (await connection.QueryAsync(sqlQuery, new { uniqueId = uniqueId })).FirstOrDefault();

        if (rawResult == null) return null;

        return MapSingleLogEntry(rawResult);
    }

    public async Task<bool> AddAsync(LogEntry logEntry)
    {
        var uniqueId = logEntry.UniqueId;
        var correlationId = logEntry.CorrelationId;
        var timestamp = logEntry.TimestampUtc;
        var offsetminutes = logEntry.OffsetMinutes;
        var level = logEntry.LevelId;
        var message = logEntry.Message;
        var source = logEntry.Source;

        var sqlInsert = """
                        insert into logs (unique_id, correlation_id, timestamp,offset_minutes, level, message, source )
                        values(@uniqueId,@correlationId, @timestamp, @offsetminutes, @level, @message,@source )
                        """;

        var connection = _context.CreateConnection();

        var rows = await connection.ExecuteAsync(sqlInsert,
            new
            {
                uniqueId,
                correlationId,
                timestamp,
                offsetminutes,
                level,
                message,
                source
            });

        return rows > 0;
    }

    public async Task<PaginatedData<LogEntry>> SearchLogsAsync(string? searchText, DateTimeOffset startDate,
        DateTimeOffset endDate, int? levelId, string? source,
        string? correlationId, int page, string? orderBy, int pageSize = 50, bool descending = true)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 1000) pageSize = 1000; // Max limit
        if (string.IsNullOrWhiteSpace(orderBy)) orderBy = "timestamp";

        var connection = _context.CreateConnection();

        var whereConditions = new List<string>();
        var parameters = new DynamicParameters();

        // Build where conditions (except searchText for FTS5)
        BuildWhereConditionsForFilters(startDate, endDate, levelId, source, correlationId, whereConditions, parameters);

        // Build ORDER BY clause
        var orderByClause = BuildOrderByClause(orderBy, descending);

        // Calculate pagination
        var offset = (page - 1) * pageSize;
        parameters.Add("@Offset", offset);
        parameters.Add("@PageSize", pageSize);

        // Use FTS5 only if searchText is provided
        var (selectQuery, countQuery) = !string.IsNullOrWhiteSpace(searchText?.Trim())
            ? BuildFtsQueries(searchText.Trim(), whereConditions, orderByClause, parameters)
            : BuildRegularQueries(searchText, whereConditions, orderByClause, parameters);

        // Execute queries
        var countTask = connection.QuerySingleAsync<int>(countQuery, parameters);
        var rawDataTask = connection.QueryAsync(selectQuery, parameters);

        await Task.WhenAll(countTask, rawDataTask);

        var totalCount = await countTask;
        var rawLogs = (await rawDataTask).ToList();

        if (rawLogs == null || rawLogs.Count <= 0)
        {
            return PaginatedData<LogEntry>.Empty();
        }

        var logs = MapTimestamps(rawLogs);

        if (logs?.Count <= 0)
        {
            return PaginatedData<LogEntry>.Empty();
        }

        return PaginatedData<LogEntry>.Create(logs ?? [], page, pageSize, totalCount);
    }

    private static LogEntry? MapSingleLogEntry(dynamic rawData)
    {
        if (rawData == null)
        {
            return null;
        }

        try
        {
            if (!Guid.TryParse(rawData?.unique_id?.ToString(), out Guid uniqueId))
            {
                // Critical field - skip if invalid
                return null;
            }

            Guid? correlationId = null;
            var correlationIdStr = rawData?.correlation_id?.ToString();
            if (!string.IsNullOrWhiteSpace(correlationIdStr))
            {
                if (Guid.TryParse(correlationIdStr, out Guid parsedCorrelationId))
                {
                    correlationId = parsedCorrelationId;
                }
            }

            if (!long.TryParse(rawData?.timestamp?.ToString(), out long timestamp))
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // Default to now
            }

            if (!int.TryParse(rawData?.offset_minutes?.ToString(), out int offsetMinutes))
            {
                offsetMinutes = 0; // Default to UTC
            }

            if (!int.TryParse(rawData?.level?.ToString(), out int level))
            {
                level = 2; // Default to Information
            }

            if (!long.TryParse(rawData?.id?.ToString(), out long id))
            {
                id = 0; // Will be handled by database
            }

            var message = rawData?.message?.ToString() ?? string.Empty;
            var source = rawData?.source?.ToString() ?? "Unknown";

            DateTimeOffset dateTimeOffset;
            try
            {
                dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(timestamp)
                    .ToOffset(TimeSpan.FromMinutes(offsetMinutes));
            }
            catch (ArgumentOutOfRangeException)
            {
                // Invalid timestamp or offset - use current time
                dateTimeOffset = DateTimeOffset.Now;
            }

            return new LogEntry
            {
                Id = id,
                UniqueId = uniqueId,
                CorrelationId = correlationId,
                Message = message,
                Timestamp = dateTimeOffset,
                OffsetMinutes = offsetMinutes,
                Source = source,
                LevelId = level
            };
        }
        catch (Exception)
        {
            // TODO: add logging
            return null;
        }
    }

    private static List<LogEntry> MapTimestamps(IEnumerable<dynamic> rawResults)
    {
        if (rawResults is null)
        {
            return [];
        }

        var logs = new List<LogEntry>();

        foreach (var rawData in rawResults)
        {
            try
            {
                //  Safe parsing with TryParse
                if (!Guid.TryParse(rawData.UniqueId?.ToString(), out Guid uniqueId))
                {
                    continue; // Skip invalid entries
                }

                Guid? correlationId = null;
                var correlationIdStr = rawData?.CorrelationId?.ToString();
                if (!string.IsNullOrWhiteSpace(correlationIdStr))
                {
                    if (Guid.TryParse(correlationIdStr, out Guid parsedCorrelationId))
                    {
                        correlationId = parsedCorrelationId;
                    }
                }

                //  Safe numeric conversions with defaults
                var timestampUtc = TryParseLong(rawData?.TimestampUtc) ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var offsetMinutes = TryParseInt(rawData?.OffsetMinutes) ?? 0;
                var id = TryParseLong(rawData?.Id) ?? 0;
                var levelId = TryParseInt(rawData?.LevelId) ?? 2; // Default to Information

                //  Safe string conversions
                var message = rawData?.Message?.ToString() ?? string.Empty;
                var source = rawData?.Source?.ToString() ?? "Unknown";

                //  Safe DateTimeOffset creation with fallback
                DateTimeOffset timestamp;
                try
                {
                    timestamp = DateTimeOffset.FromUnixTimeSeconds(timestampUtc)
                        .ToOffset(TimeSpan.FromMinutes(offsetMinutes));
                }
                catch (ArgumentOutOfRangeException e)
                {
                    // Invalid timestamp/offset - use current time with specified offset
                    timestamp = DateTimeOffset.Now.ToOffset(TimeSpan.FromMinutes(offsetMinutes));
                }

                var logEntry = new LogEntry
                {
                    Id = id,
                    UniqueId = uniqueId,
                    CorrelationId = correlationId,
                    Message = message,
                    Timestamp = timestamp,
                    OffsetMinutes = offsetMinutes,
                    Source = source,
                    LevelId = levelId
                };

                logs.Add(logEntry);
            }
            catch (Exception)
            {
                // TODO: log this
                continue;
            }
        }

        return logs;
    }

    private static long? TryParseLong(object? value)
    {
        if (value == null) return null;
        return long.TryParse(value.ToString(), out var result) ? result : null;
    }

    private static int? TryParseInt(object? value)
    {
        if (value == null) return null;
        return int.TryParse(value.ToString(), out var result) ? result : null;
    }

    #region Helper Methods

    private static void BuildWhereConditionsForFilters(DateTimeOffset startDate, DateTimeOffset endDate,
        int? levelId, string? source, string? correlationId, List<string> whereConditions, DynamicParameters parameters)
    {
        // ✅ Safe timestamp conversion
        try
        {
            whereConditions.Add("l.timestamp >= @StartTimestamp");
            parameters.Add("@StartTimestamp", startDate.ToUnixTimeSeconds());

            whereConditions.Add("l.timestamp <= @EndTimestamp");
            parameters.Add("@EndTimestamp", endDate.ToUnixTimeSeconds());
        }
        catch (ArgumentOutOfRangeException)
        {
            // Invalid date range - use reasonable defaults
            whereConditions.Add("l.timestamp >= @StartTimestamp");
            parameters.Add("@StartTimestamp", DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeSeconds());

            whereConditions.Add("l.timestamp <= @EndTimestamp");
            parameters.Add("@EndTimestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

       
        if (levelId is >= 0 and <= 5) // Valid log levels
        {
            whereConditions.Add("l.level = @Level");
            parameters.Add("@Level", levelId);
        }

        
        if (!string.IsNullOrWhiteSpace(source) && source.Length <= 100) // Reasonable length limit
        {
            whereConditions.Add("l.source LIKE @Source");
            parameters.Add("@Source", $"%{source.Trim()}%");
        }
        
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            // Validate it's a GUID format
            if (Guid.TryParse(correlationId, out _))
            {
                whereConditions.Add("l.correlation_id = @CorrelationId");
                parameters.Add("@CorrelationId", correlationId);
            }
        }
    }

    private static string BuildMessageSelectClause(string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            // get only first 300 characters
            return @"
                CASE 
                    WHEN LENGTH(l.message) <= 300 THEN l.message
                    ELSE SUBSTR(l.message, 1, 300) || '...'
                END as Message";
        }

        // Get first word from search text for SQL
        var firstWord = searchText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ??
                        searchText;

        // get first 300 characters AROUND of searched words
        return $@"
            CASE 
                WHEN LENGTH(l.message) <= 300 THEN l.message
                WHEN INSTR(LOWER(l.message), LOWER(@SearchWord)) = 0 THEN 
                    SUBSTR(l.message, 1, 300) || '...'
                ELSE
                    CASE 
                        WHEN INSTR(LOWER(l.message), LOWER(@SearchWord)) <= 100 THEN
                            -- Search word is near beginning, start from position 1
                            CASE 
                                WHEN LENGTH(l.message) <= 300 THEN l.message
                                ELSE SUBSTR(l.message, 1, 300) || '...'
                            END
                        ELSE
                            -- Search word is further in, center around it
                            CASE 
                                WHEN (INSTR(LOWER(l.message), LOWER(@SearchWord)) - 100 + 300) >= LENGTH(l.message) THEN
                                    '...' || SUBSTR(l.message, INSTR(LOWER(l.message), LOWER(@SearchWord)) - 100)
                                ELSE
                                    '...' || SUBSTR(l.message, INSTR(LOWER(l.message), LOWER(@SearchWord)) - 100, 300) || '...'
                            END
                    END
            END as Message";
    }

    private static string BuildOrderByClause(string? orderBy, bool descending)
    {
        var direction = descending ? "DESC" : "ASC";
        
        var safeOrderBy = orderBy?.ToLowerInvariant()?.Trim();
        return safeOrderBy switch
        {
            "level" => $"l.level {direction}, l.timestamp DESC",
            "source" => $"l.source {direction}, l.timestamp DESC",
            "timestamp" => $"l.timestamp {direction}",
            _ => "l.timestamp DESC"
        };
    }

    private static (string selectQuery, string countQuery) BuildFtsQueries(string searchText,
        List<string> whereConditions, string orderByClause, DynamicParameters parameters)
    {
        // Prepare FTS5 search term
        var searchTerm = EscapeFtsSearchTerm(searchText.Trim());
        parameters.Add("@SearchText", searchTerm);

        // Add first word for message truncation
        var firstWord = searchText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? searchText;
        parameters.Add("@SearchWord", firstWord);

        var messageSelectClause = BuildMessageSelectClause(searchText);

        var baseJoin = $@"
            FROM logs_fts fts
            INNER JOIN logs l ON l.id = fts.rowid";

        var whereClause = whereConditions.Count > 0
            ? "WHERE fts.logs_fts MATCH @SearchText AND " + string.Join(" AND ", whereConditions)
            : "WHERE fts.logs_fts MATCH @SearchText";

        var selectQuery = $@"
            SELECT l.id as Id, 
                   l.unique_id as UniqueId, 
                   l.correlation_id as CorrelationId,
                   {messageSelectClause},
                   l.timestamp as TimestampUtc,
                   l.offset_minutes as OffsetMinutes, 
                   l.source as Source, 
                   l.level as LevelId
            {baseJoin}
            {whereClause}
            ORDER BY {orderByClause}
            LIMIT @PageSize OFFSET @Offset";

        var countQuery = $@"
            SELECT COUNT(*)
            {baseJoin}
            {whereClause}";

        return (selectQuery, countQuery);
    }

    private static (string selectQuery, string countQuery) BuildRegularQueries(string? searchText,
        List<string> whereConditions, string orderByClause, DynamicParameters parameters)
    {
        var messageSelectClause = BuildMessageSelectClause(searchText);

        // Add search word parameter if needed
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var firstWord = searchText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ??
                            searchText;
            parameters.Add("@SearchWord", firstWord);
        }

        var whereClause = whereConditions.Count > 0
            ? "WHERE " + string.Join(" AND ", whereConditions)
            : "";

        var selectQuery = $@"
            SELECT id as Id, 
                   unique_id as UniqueId, 
                   correlation_id as CorrelationId, 
                   {messageSelectClause},
                   timestamp as TimestampUtc, 
                   offset_minutes as OffsetMinutes, 
                   source as Source, 
                   level as LevelId 
            FROM logs l 
            {whereClause} 
            ORDER BY {orderByClause} 
            LIMIT @PageSize OFFSET @Offset";

        var countQuery = $@"SELECT COUNT(*) FROM logs l {whereClause}";

        return (selectQuery, countQuery);
    }

    private static string EscapeFtsSearchTerm(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return string.Empty;
        }
        
        var trimmed = searchText.Trim();
        if (trimmed.Length > 500) // Prevent extremely long search terms
        {
            trimmed = trimmed.Substring(0, 500);
        }

        var escaped = trimmed
            .Replace("\"", "\"\"") // Double quotes
            .Replace("'", "''"); // Single quotes

        if (trimmed.Contains(' '))
        {
            return $"\"{escaped}\"";
        }

        return $"{escaped}*";
    }

    #endregion
}