using System.Collections.Concurrent;
using LoQi.Application.DTOs;
using LoQi.Application.Services.Log;
using LoQi.Infrastructure;
using LoQi.Infrastructure.Models;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace LoQi.API.BackgroundServices.Redis;

/// <summary>
/// Background service that processes successfully parsed logs from Redis Stream
/// and performs optimized bulk inserts to SQLite database using batching strategy
/// </summary>
public class RawLogsConsumerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RawLogsConsumerService> _logger;
    private readonly RedisStreamConfig _config;
    private readonly string _consumerName;

    // this service makes log parsing, constructor injection is enough
    private readonly ILogParserService _logParserService;

    //  Batching configuration
    private const int MaxBatchSize = 100; // 100 mesaj toplandığında flush
    private const int FlushIntervalSeconds = 10; // 10 saniyede bir flush
    private const int ConsumerDelayMs = 1000; // Consumer polling delay

    //  Batch storage
    private readonly ConcurrentQueue<LogBatchItem> _messageBatch = new();
    private readonly SemaphoreSlim _batchSemaphore = new(1, 1);

    private readonly IRedisStreamService _redisStreamService;

    public RawLogsConsumerService(
        IServiceProvider serviceProvider,
        ILogger<RawLogsConsumerService> logger,
        IOptions<RedisStreamConfig> config,
        ILogParserService logParserService,
        IRedisStreamService redisStreamService
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _logParserService = logParserService;
        _redisStreamService = redisStreamService;
        _config = config.Value;
        _consumerName = $"rawlogs-consumer-{Environment.MachineName}-{Guid.NewGuid():N}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            $"{nameof(RawLogsConsumerService)} started with consumer: {_consumerName}, MaxBatch: {{MaxBatchSize}}, FlushInterval: {{FlushIntervalSeconds}}s");

        //  Timer-based flush task
        var flushTask = StartPeriodicFlushAsync(stoppingToken);

        //  Message consumer task
        var consumerTask = StartMessageConsumerAsync(stoppingToken);

        try
        {
            //  Her iki task'ı paralel çalıştır
            await Task.WhenAny(flushTask, consumerTask);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ProcessedLogsConsumerService cancellation requested");
        }
        finally
        {
            //  Shutdown sırasında kalan mesajları flush et
            await FlushRemainingMessages();
            _batchSemaphore.Dispose();
            _logger.LogInformation("ProcessedLogsConsumerService stopped");
        }
    }

    /// <summary>
    /// Timer-based periodic flush (her 10 saniyede bir)
    /// </summary>
    private async Task StartPeriodicFlushAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(FlushIntervalSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await FlushBatchIfNotEmpty("Timer");
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    /// <summary>
    /// Continuous message consumer from Redis Stream
    /// </summary>
    private async Task StartMessageConsumerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                //  Redis'ten mesajları oku
                var messages = await _redisStreamService.ReadMessagesAsync(
                    "raw-logs",
                    _consumerName,
                    _config.ConsumerGroups["raw-logs"].BatchSize,
                    _config.ConsumerGroups["raw-logs"].BlockTimeMs);

                if (messages.Length > 0)
                {
                    //  Mesajları batch'e ekle
                    await AddMessagesToBatch(messages);

                    _logger.LogDebug("Added {Count} messages to batch. Current batch size: {BatchSize}",
                        messages.Length, _messageBatch.Count);

                    continue;
                }

                // Mesaj yoksa kısa bir süre bekle
                await Task.Delay(ConsumerDelayMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading messages from Redis stream");
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Add messages to batch and trigger size-based flush if needed
    /// </summary>
    private async Task AddMessagesToBatch(StreamEntry[] messages)
    {
        //  Mesajları batch'e ekle
        foreach (var message in messages)
        {
            var logMessage = ParseLogMessage(message);
            var batchItem = new LogBatchItem
            {
                Message = logMessage,
                MessageId = message.Id.ToString()
            };

            _messageBatch.Enqueue(batchItem);
        }

        //  Size-based flush check (100 mesaj doldu mu?)
        if (_messageBatch.Count >= MaxBatchSize)
        {
            await FlushBatchIfNotEmpty($"Size({_messageBatch.Count})");
        }
    }

    /// <summary>
    /// Flush batch if not empty with thread safety
    /// </summary>
    private async Task FlushBatchIfNotEmpty(string trigger)
    {
        if (_messageBatch.IsEmpty) return;

        await _batchSemaphore.WaitAsync();
        try
        {
            if (_messageBatch.IsEmpty) return; // Double-check

            //  Batch'teki tüm mesajları al
            var batchItems = new List<LogBatchItem>();
            while (_messageBatch.TryDequeue(out var item))
            {
                batchItems.Add(item);
            }

            if (batchItems.Count == 0) return;

            await ProcessBatch(batchItems, trigger);
        }
        finally
        {
            _batchSemaphore.Release();
        }
    }

    /// <summary>
    /// Process a batch of messages - Final simplified version
    /// </summary>
    private async Task ProcessBatch(List<LogBatchItem> batchItems, string trigger)
    {
        try
        {
            _logger.LogInformation("Processing batch of {Count} messages (triggered by: {Trigger})",
                batchItems.Count, trigger);

            // Convert to RawLogDto and sort by Redis Stream ID (chronological order)
            var rawLogDtos = batchItems
                .Select(item => new RawLogDto(
                    OriginalData: item.Message.OriginalData,
                    RedisStreamId: item.Message.Id.ToString()
                ))
                .OrderBy(dto => dto.RedisStreamId) // Redis Stream ID is chronologically ordered
                .ToList();

            if (rawLogDtos.Count == 0)
            {
                _logger.LogInformation("No messages to process!");
                return;
            }

            // Parse raw log DTOs to LogEntry objects
            var logs = await _logParserService.ConvertToLogEntryAsync(rawLogDtos);

            // MessageIds for acknowledgment
            var messageIds = batchItems.Select(item => item.MessageId).ToArray();

            if (logs?.Count == 0)
            {
                _logger.LogWarning("No valid logs parsed from {Count} raw messages", rawLogDtos.Count);

                // ACK poison messages to prevent infinite retry
                await _redisStreamService.AcknowledgeMessagesAsync("raw-logs", messageIds);
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var logService = scope.ServiceProvider.GetRequiredService<ILogService>();

            // Bulk insert to SQLite with RedisStreamId
            var isSuccessful = await logService.AddLogsBatchAsync(logs);

            if (isSuccessful)
            {
                // Acknowledge all messages in batch
                await _redisStreamService.AcknowledgeMessagesAsync("raw-logs", messageIds);
                _logger.LogInformation(
                    "Successfully processed batch: {TotalMessages} messages → {ValidLogs} logs inserted",
                    batchItems.Count, logs?.Count ?? 0);
            }
            else
            {
                _logger.LogError("Failed to insert batch of {Count} logs to database", logs?.Count ?? 0);
                throw new Exception("Database bulk insert failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch of {Count} messages", batchItems.Count);

            // Re-queue for retry (with exponential backoff ideally)
            foreach (var item in batchItems)
            {
                _messageBatch.Enqueue(item);
            }

            throw; // Re-throw for upper level handling
        }
    }

    /// <summary>
    /// Flush remaining messages during shutdown
    /// </summary>
    private async Task FlushRemainingMessages()
    {
        if (!_messageBatch.IsEmpty)
        {
            _logger.LogInformation("Flushing {Count} remaining messages during shutdown", _messageBatch.Count);
            await FlushBatchIfNotEmpty("Shutdown");
        }
    }

    /// <summary>
    /// Parse Redis stream entry to log message
    /// </summary>
    private static LogStreamMessage ParseLogMessage(StreamEntry entry)
    {
        var fields = entry.Values.ToDictionary(kv => kv.Name.ToString(), kv => kv.Value.ToString());

        return new LogStreamMessage
        {
            Id = entry.Id,
            OriginalData = fields.GetValueOrDefault("originalData", ""),
            Status = Enum.Parse<LogProcessingStatus>(fields.GetValueOrDefault("status", "failed"), true),
            ParsedData = fields.GetValueOrDefault("parsedData"),
            ErrorInfo = fields.GetValueOrDefault("errorInfo"),
            Attempts = int.Parse(fields.GetValueOrDefault("attempts", "1"))
        };
    }
}

/// <summary>
/// Batch item containing message and its Redis Stream ID
/// </summary>
public class LogBatchItem
{
    public LogStreamMessage Message { get; set; } = null!;
    public string MessageId { get; set; } = null!;
}