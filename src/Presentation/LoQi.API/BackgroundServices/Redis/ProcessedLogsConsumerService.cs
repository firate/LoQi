using LoQi.Infrastructure;
using LoQi.Infrastructure.Models;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace LoQi.API.BackgroundServices.Redis;

/// <summary>
/// Background service that processes successfully parsed logs from Redis Stream
/// and performs bulk inserts to SQLite database
/// </summary>
public class ProcessedLogsConsumerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProcessedLogsConsumerService> _logger;
    private readonly RedisStreamConfig _config;
    private readonly string _consumerName;

    public ProcessedLogsConsumerService(
        IServiceProvider serviceProvider,
        ILogger<ProcessedLogsConsumerService> logger,
        IOptions<RedisStreamConfig> config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config.Value;
        _consumerName = $"processed-consumer-{Environment.MachineName}-{Guid.NewGuid():N}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProcessedLogsConsumerService started with consumer name: {ConsumerName}",
            _consumerName);

        while (!stoppingToken.IsCancellationRequested)
        {
            // try
            // {
            //     using var scope = _serviceProvider.CreateScope();
            //     var redisStreamService = scope.ServiceProvider.GetRequiredService<IRedisStreamService>();
            //
            //     // Read batch of processed logs
            //     var messages = await redisStreamService.ReadMessagesAsync(
            //         "processed-logs",
            //         _consumerName,
            //         _config.ConsumerGroups["processed-logs"].BatchSize,
            //         _config.ConsumerGroups["processed-logs"].BlockTimeMs);
            //
            //     if (messages.Length <= 0) continue;
            //
            //     var logMessages = messages.Select(ParseLogMessage).ToList();
            //
            //     // TODO: Bulk insert to SQLite using ILogService
            //     //await _logService.BulkInsertLogsAsync(logMessages);
            //
            //     // Acknowledge processed messages
            //     var messageIds = messages.Select(m => m.Id.ToString()).ToArray();
            //     await redisStreamService.AcknowledgeMessagesAsync("processed-logs", messageIds);
            //
            //     _logger.LogInformation("Processed {Count} log messages", messages.Length);
            // }
            // catch (OperationCanceledException)
            // {
            //     break;
            // }
            // catch (Exception ex)
            // {
            //     _logger.LogError(ex, "Error processing logs from Redis stream");
            //     await Task.Delay(5000, stoppingToken); // Wait before retry
            // }
        }

        _logger.LogInformation("ProcessedLogsConsumerService stopped");
    }

    private static LogStreamMessage ParseLogMessage(StreamEntry entry)
    {
        var fields = entry.Values.ToDictionary(kv => kv.Name.ToString(), kv => kv.Value.ToString());

        return new LogStreamMessage
        {
            Id = entry.Id,
            OriginalData = fields.GetValueOrDefault("originalData", ""),
            Status = Enum.TryParse<LogProcessingStatus>(fields.GetValueOrDefault("status", "failed"), out var statusEnum) ? statusEnum: LogProcessingStatus.Failed,
            ParsedData = fields.GetValueOrDefault("parsedData"),
            ErrorInfo = fields.GetValueOrDefault("errorInfo"),
            //Timestamp = long.TryParse(fields.GetValueOrDefault("timestamp", "0"), out var timeStamp) ? timeStamp: 0,
            Attempts = int.TryParse(fields.GetValueOrDefault("attempts", "1"), out var attemps) ? attemps : 1
        };
    }
}