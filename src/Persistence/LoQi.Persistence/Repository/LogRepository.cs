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
        var sqlQuery = "select * from logs where unique_id = @uniqueId";

        var connection = _context.CreateConnection();

        var logEntry = (await connection.QueryAsync<LogEntry>(sqlQuery, new { unique_id = uniqueId })).FirstOrDefault();

        return logEntry;
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

    public async Task<PagedResult<LogEntry>> SearchLogsAsync(string searchText, DateTimeOffset startDate,
        DateTimeOffset endDate, int levelId, string? source,
        string? correlationId, int page, string orderBy, int pageSize = 50, bool descending = true)
    {
        var connection = _context.CreateConnection();

        var whereConditions = new List<string>();
        var parameters = new DynamicParameters();

        whereConditions.Add("l.timestamp >= @StartTimestamp");
        parameters.Add("@StartTimestamp", startDate.ToUnixTimeSeconds());

        whereConditions.Add("l.timestamp <= @EndTimestamp");
        parameters.Add("@EndTimestamp", endDate.ToUnixTimeSeconds());

        whereConditions.Add("l.level = @Level");
        parameters.Add("@Level", levelId);

        // Source filter
        if (!string.IsNullOrWhiteSpace(source))
        {
            whereConditions.Add("l.source LIKE @Source");
            parameters.Add("@Source", $"%{source.Trim()}%");
        }

        // Correlation ID filter
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            whereConditions.Add("l.correlation_id = @CorrelationId");
            parameters.Add("@CorrelationId", correlationId);
        }

        // Full text search parameter
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            // Escape FTS5 special characters and prepare search term
            var searchTerm = EscapeFtsSearchTerm(searchText.Trim());
            parameters.Add("@SearchText", searchTerm);
        }

        // Build ORDER BY clause
        var orderByClause = BuildOrderByClause(orderBy, descending);

        // Calculate pagination
        var offset = (page - 1) * pageSize;
        parameters.Add("@Offset", offset);
        parameters.Add("@PageSize", pageSize);

        // Determine if we need FTS5 or regular search
        var (selectQuery, countQuery) = searchText?.Trim().Length > 0
            ? BuildFtsQueries(whereConditions, orderByClause)
            : BuildRegularQueries(whereConditions, orderByClause);

        // Execute queries
        var countTask = connection.QuerySingleAsync<int>(countQuery, parameters);
        var logsTask = connection.QueryAsync<LogEntry>(selectQuery, parameters);

        await Task.WhenAll(countTask, logsTask);

        var totalCount = await countTask;
        var logs = (await logsTask).ToList();

        var fixedLogs = MapTimestamps(logs);
        
        if (logs?.Count < 0)
        {
            return PagedResult<LogEntry>.Empty();
        }

        return PagedResult<LogEntry>.Create(logs ?? [], totalCount, page, pageSize);
    }


    #region MyRegion

    private static string BuildOrderByClause(string orderBy, bool descending)
    {
        var direction = descending ? "DESC" : "ASC";

        return orderBy.ToLowerInvariant() switch
        {
            "level" => $"l.level {direction}, l.timestamp DESC",
            "source" => $"l.source {direction}, l.timestamp DESC",
            "timestamp" or _ => $"l.timestamp {direction}"
        };
    }

    private static (string selectQuery, string countQuery) BuildFtsQueries(List<string> whereConditions,
        string orderByClause)
    {
        var baseJoin = @"
            FROM logs_fts fts
            INNER JOIN logs l ON l.id = fts.rowid";

        var whereClause = whereConditions.Count > 0
            ? "WHERE fts.logs_fts MATCH @SearchText AND " + string.Join(" AND ", whereConditions)
            : "WHERE fts.logs_fts MATCH @SearchText";

        var selectQuery = $@"
            SELECT l.id as Id, 
                   l.unique_id as UniqueId, 
                   l.correlation_id as CorrelationId,
                   l.message as Message, 
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

    private static (string selectQuery, string countQuery) BuildRegularQueries(List<string> whereConditions,
        string orderByClause)
    {
        var whereClause = whereConditions.Count > 0
            ? "WHERE " + string.Join(" AND ", whereConditions)
            : "";

        var selectQuery = $@"
            SELECT id as Id, 
                   unique_id as UniqueId, 
                   correlation_id as CorrelationId,
                   message as Message, 
                   timestamp as TimestampUtc,
                   offset_minutes as OffsetMinutes, 
                   source as Source, 
                   level as LevelId
            FROM logs l
            {whereClause}
            ORDER BY {orderByClause}
            LIMIT @PageSize OFFSET @Offset";

        var countQuery = $@"
            SELECT COUNT(*)
            FROM logs l
            {whereClause}";

        return (selectQuery, countQuery);
    }

    private static string EscapeFtsSearchTerm(string searchText)
    {
        // FTS5 özel karakterlerini escape et
        var escaped = searchText
            .Replace("\"", "\"\"") // Double quotes
            .Replace("'", "''"); // Single quotes

        // Eğer boşluk içeriyorsa phrase search yap
        if (searchText.Contains(' '))
        {
            return $"\"{escaped}\"";
        }

        // Prefix search için * ekle (isteğe bağlı)
        return $"{escaped}*";
    }

    // LogEntry entity'sindeki Timestamp property'sini DateTimeOffset'e çevirmek için
    private static List<LogEntry> MapTimestamps(IEnumerable<dynamic> results)
    {
        return results.Select(r => new LogEntry
        {
            Id = r.Id,
            UniqueId = Guid.Parse(r.UniqueId),
            CorrelationId = string.IsNullOrEmpty(r.CorrelationId) ? null : Guid.Parse(r.CorrelationId),
            Message = r.Message,
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(r.TimestampUtc)
                .ToOffset(TimeSpan.FromMinutes(r.OffsetMinutes)),
            OffsetMinutes = r.OffsetMinutes,
            Source = r.Source,
            LevelId = r.LevelId
        }).ToList();
    }
    
    private static List<LogEntry> MapTimestamps(IEnumerable<LogEntry> results)
    {
        return results.Select(r => new LogEntry
        {
            Id = r.Id,
            UniqueId = r.UniqueId,
            CorrelationId = r.CorrelationId,
            Message = r.Message,
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(r.TimestampUtc)
                .ToOffset(TimeSpan.FromMinutes(r.OffsetMinutes)),
            OffsetMinutes = r.OffsetMinutes,
            Source = r.Source,
            LevelId = r.LevelId
        }).ToList();
    }

    #endregion
}