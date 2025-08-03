using LoQi.Infrastructure;
using LoQi.Infrastructure.Models;
using Microsoft.Extensions.Options;

namespace LoQi.API.BackgroundServices.Redis;

/// <summary>
/// Background service that retries logs with transient errors
/// </summary>
public class RetryLogsConsumerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RetryLogsConsumerService> _logger;
    private readonly RedisStreamConfig _config;
    private readonly string _consumerName;

    public RetryLogsConsumerService(
        IServiceProvider serviceProvider,
        ILogger<RetryLogsConsumerService> logger,
        IOptions<RedisStreamConfig> config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config.Value;
        _consumerName = $"retry-consumer-{Environment.MachineName}-{Guid.NewGuid():N}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RetryLogsConsumerService started with consumer name: {ConsumerName}", _consumerName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var redisStreamService = scope.ServiceProvider.GetRequiredService<IRedisStreamService>();
                
                // Read retry log messages
                var messages = await redisStreamService.ReadMessagesAsync(
                    "retry-logs", 
                    _consumerName,
                    _config.ConsumerGroups["retry-logs"].BatchSize,
                    _config.ConsumerGroups["retry-logs"].BlockTimeMs);

                if (messages.Length > 0)
                {
                    // var logMessages = messages.Select(m => m.ParseLogMessage()).ToList();
                    //
                    // // TODO: Retry log parsing logic
                    // foreach (var logMessage in logMessages)
                    // {
                    //     if (logMessage.Attempts < _config.ConsumerGroups["retry-logs"].MaxRetries)
                    //     {
                    //         // TODO: Attempt to reparse the log
                    //         // If successful -> add to processed-logs stream
                    //         // If failed again -> increment attempts and re-add to retry-logs or move to failed-logs
                    //     }
                    //     else
                    //     {
                    //         // Max retries reached, move to failed-logs
                    //         await redisStreamService.AddLogMessageAsync(
                    //             logMessage.OriginalData,
                    //             LogProcessingStatus.Failed,
                    //             errorInfo: $"Max retries ({logMessage.Attempts}) exceeded",
                    //             attempts: logMessage.Attempts);
                    //     }
                    // }
                    //
                    // // Acknowledge processed messages
                    // var messageIds = messages.Select(m => m.Id.ToString()).ToArray();
                    // await redisStreamService.AcknowledgeMessagesAsync("retry-logs", messageIds);
                    //
                    // _logger.LogInformation("Processed {Count} retry log messages", messages.Length);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing retry logs from Redis stream");
                await Task.Delay(7500, stoppingToken); // Medium wait for retries
            }
        }

        _logger.LogInformation("RetryLogsConsumerService stopped");
    }
}