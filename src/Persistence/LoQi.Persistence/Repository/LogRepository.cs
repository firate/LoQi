using Dapper;
using LoQi.Application.Repository;
using LoQi.Domain;
using LoQi.Domain.Enums;

namespace LoQi.Persistence.Repository;

public class LogRepository : ILogRepository
{
    private readonly DataContext _context;

    public LogRepository(DataContext context)
    {
        _context = context;
    }

    public async Task<List<LogEntry>?> SearchLogs(string searchText, DateTimeOffset beginDate, DateTimeOffset endDate,
        LogLevel logLevel)
    {
        var bDate = beginDate.ToUnixTimeSeconds();
        var eDate = endDate.ToUnixTimeSeconds();
        var level = (int)logLevel;

        var sql = @"SELECT l.* FROM logs l 
                    JOIN logs_fts fts ON l.id = fts.rowid 
                    WHERE logs_fts MATCH @searchText
                    and timestamp >= @bDate
                    and timestamp <= @eDate
                    and level = @level
                    ORDER BY l.timestamp DESC";

        var connection = _context.CreateConnection();

        return (await connection.QueryAsync<LogEntry>(sql, new
        {
            searchText,
            bDate,
            eDate,
            level
        })).ToList();
    }

    public async Task<LogEntry?> GetLogByIdAsync(long id)
    {
        var sqlQuery = "select * from logs where id = @id";

        var connection = _context.CreateConnection();

        var logEntry = (await connection.QueryAsync<LogEntry>(sqlQuery, new { id = id })).FirstOrDefault();

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

        var sqlInsert = @"insert into logs 
                        (unique_id, correlation_id, timestamp,offset_minutes, level, message, source )
                        values(@uniqueId,@correlationId, @timestamp, @offsetminutes, @level, @message,@source )
                        ";

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
}