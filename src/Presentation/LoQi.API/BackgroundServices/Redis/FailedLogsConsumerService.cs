using LoQi.Infrastructure;
using LoQi.Infrastructure.Extensions;
using LoQi.Infrastructure.Models;
using Microsoft.Extensions.Options;

namespace LoQi.API.BackgroundServices.Redis;

/// <summary>
/// Background service that handles failed log parsing attempts
/// </summary>
public class FailedLogsConsumerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FailedLogsConsumerService> _logger;
    private readonly RedisStreamConfig _config;
    private readonly string _consumerName;

    public FailedLogsConsumerService(
        IServiceProvider serviceProvider,
        ILogger<FailedLogsConsumerService> logger,
        IOptions<RedisStreamConfig> config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config.Value;
        _consumerName = $"failed-consumer-{Environment.MachineName}-{Guid.NewGuid():N}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FailedLogsConsumerService started with consumer name: {ConsumerName}", _consumerName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var redisStreamService = scope.ServiceProvider.GetRequiredService<IRedisStreamService>();
                
                // Read failed log messages
                var messages = await redisStreamService.ReadMessagesAsync(
                    "failed-logs", 
                    _consumerName,
                    _config.ConsumerGroups["failed-logs"].BatchSize,
                    _config.ConsumerGroups["failed-logs"].BlockTimeMs);

                if (messages.Length > 0)
                {
                    var logMessages = messages.Select(m => m.ParseLogMessage()).ToList();
                    
                    // TODO: Handle failed logs - log to file, send alerts, etc.
                    foreach (var logMessage in logMessages)
                    {
                        _logger.LogWarning("Failed to parse log: {OriginalData}, Error: {ErrorInfo}", 
                            logMessage.OriginalData, logMessage.ErrorInfo);
                    }

                    // Acknowledge processed messages
                    var messageIds = messages.Select(m => m.Id.ToString()).ToArray();
                    await redisStreamService.AcknowledgeMessagesAsync("failed-logs", messageIds);

                    _logger.LogInformation("Handled {Count} failed log messages", messages.Length);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling failed logs from Redis stream");
                await Task.Delay(10000, stoppingToken); // Longer wait for failed logs
            }
        }

        _logger.LogInformation("FailedLogsConsumerService stopped");
    }
}