using LoQi.Infrastructure.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace LoQi.Infrastructure;

/// <summary>
/// Redis Stream service implementation for LoQi log processing
/// </summary>
public class RedisStreamService : IRedisStreamService
{
    private readonly IDatabase _database;
    // private readonly ILogger<RedisStreamService> _logger;
    private readonly RedisStreamConfig _config;
    private readonly string _streamKey;

    public RedisStreamService(
        IConnectionMultiplexer redis,
        IOptions<RedisStreamConfig> config
        // ,ILogger<RedisStreamService> logger
        )
    {
        _database = redis.GetDatabase();
        _config = config.Value;
        // _logger = logger;
        _streamKey = _config.StreamName;
    }

    /// <summary>
    /// Add a log message to the stream after processing attempt
    /// </summary>
    public async Task<string> AddLogMessageAsync(
        string originalData, 
        LogProcessingStatus status, 
        string? parsedData = null, 
        string? errorInfo = null, 
        int attempts = 1)
    {
        try
        {
            var fields = new NameValueEntry[]
            {
                new("originalData", originalData),
                new("status", status.ToString().ToLowerInvariant()),
                new("timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                new("attempts", attempts)
            };

            // Add optional fields
            var fieldList = fields.ToList();

            if (!string.IsNullOrEmpty(parsedData))
            {
                fieldList.Add(new("parsedData", parsedData));
            }


            if (!string.IsNullOrEmpty(errorInfo))
            {
                fieldList.Add(new("errorInfo", errorInfo));
            }
                

            var messageId = await _database.StreamAddAsync(_streamKey, fieldList.ToArray());
            
            // _logger.LogTrace("Added message to stream {StreamKey} with ID {MessageId}, Status: {Status}", 
            //     _streamKey, messageId, status);

            // Trim stream if it gets too large
            if (messageId.ToString().EndsWith("-0")) // First message in this millisecond, good time to trim
            {
                _ = Task.Run(async () => await TrimStreamIfNeeded());
            }

            return messageId!;
        }
        catch (Exception ex)
        {
           // _logger.LogError(ex, "Failed to add message to Redis stream {StreamKey}", _streamKey);
            throw;
        }
    }

    /// <summary>
    /// Read messages from a specific consumer group
    /// </summary>
    public async Task<StreamEntry[]> ReadMessagesAsync(
        string consumerGroup, 
        string consumerName, 
        int batchSize = 100, 
        int blockTimeMs = 1000)
    {
        try
        {
            // Ensure consumer group exists
            await EnsureConsumerGroupExists(consumerGroup);

            StreamEntry[] result;

            if (blockTimeMs > 0)
            {
                // Blocking read with timeout
                var streamResults = await _database.StreamReadGroupAsync(
                    new StreamPosition[] { new(_streamKey, ">") },
                    consumerGroup,
                    consumerName,
                    countPerStream: batchSize,
                    flags: CommandFlags.None);

                result = streamResults.Length > 0 ? streamResults[0].Entries : Array.Empty<StreamEntry>();
            }
            else
            {
                // Non-blocking read
                result = await _database.StreamReadGroupAsync(
                    _streamKey,
                    consumerGroup,
                    consumerName,
                    ">", // Only new messages
                    count: batchSize,
                    flags: CommandFlags.None);
            }

            // _logger.LogTrace("Read {Count} messages from consumer group {ConsumerGroup} for consumer {ConsumerName}", 
            //     result.Length, consumerGroup, consumerName);

            return result;
        }
        catch (Exception ex)
        {
           // _logger.LogError(ex, "Failed to read from consumer group {ConsumerGroup}", consumerGroup);
            return Array.Empty<StreamEntry>();
        }
    }

    /// <summary>
    /// Acknowledge processed messages
    /// </summary>
    public async Task<long> AcknowledgeMessagesAsync(string consumerGroup, params string[] messageIds)
    {
        if (messageIds == null || messageIds.Length == 0)
            return 0;

        try
        {
            var redisValues = messageIds.Select(id => (RedisValue)id).ToArray();
            var acknowledged = await _database.StreamAcknowledgeAsync(_streamKey, consumerGroup, redisValues);
            
            // _logger.LogTrace("Acknowledged {Count} messages for consumer group {ConsumerGroup}", 
            //     acknowledged, consumerGroup);

            return acknowledged;
        }
        catch (Exception ex)
        {
            // _logger.LogError(ex, "Failed to acknowledge messages for consumer group {ConsumerGroup}", consumerGroup);
            throw;
        }
    }

    /// <summary>
    /// Create consumer group if it doesn't exist
    /// </summary>
    public async Task<bool> CreateConsumerGroupAsync(string consumerGroup, string startPosition = "$")
    {
        try
        {
            await _database.StreamCreateConsumerGroupAsync(_streamKey, consumerGroup, startPosition, createStream: true);
            // _logger.LogInformation("Created consumer group {ConsumerGroup} for stream {StreamKey}", 
            //     consumerGroup, _streamKey);
            return true;
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Consumer group already exists
           // _logger.LogTrace("Consumer group {ConsumerGroup} already exists", consumerGroup);
            return false;
        }
        catch (Exception ex)
        {
            // _logger.LogError(ex, "Failed to create consumer group {ConsumerGroup}", consumerGroup);
            throw;
        }
    }

    /// <summary>
    /// Get pending message count for a consumer group
    /// </summary>
    public async Task<long> GetPendingCountAsync(string consumerGroup)
    {
        try
        {
            var pending = await _database.StreamPendingAsync(_streamKey, consumerGroup);
            return pending.PendingMessageCount;
        }
        catch (Exception ex)
        {
            // _logger.LogError(ex, "Failed to get pending count for consumer group {ConsumerGroup}", consumerGroup);
            return 0;
        }
    }

    /// <summary>
    /// Get pending messages for a specific consumer (for recovery)
    /// </summary>
    public async Task<StreamEntry[]> GetPendingMessagesAsync(string consumerGroup, string consumerName)
    {
        try
        {
            var pendingInfo = await _database.StreamPendingMessagesAsync(
                _streamKey, consumerGroup, 100, consumerName);

            if (pendingInfo.Length == 0)
                return Array.Empty<StreamEntry>();

            var messageIds = pendingInfo.Select(p => p.MessageId).ToArray();
            var claimedMessages = await _database.StreamClaimAsync(
                _streamKey, consumerGroup, consumerName, 0, messageIds);

            // _logger.LogInformation("Retrieved {Count} pending messages for consumer {ConsumerName}", 
            //     claimedMessages.Length, consumerName);

            return claimedMessages;
        }
        catch (Exception ex)
        {
            // _logger.LogError(ex, "Failed to get pending messages for consumer {ConsumerName}", consumerName);
            return Array.Empty<StreamEntry>();
        }
    }

    /// <summary>
    /// Claim pending messages from dead consumers
    /// </summary>
    public async Task<StreamEntry[]> ClaimPendingMessagesAsync(
        string consumerGroup, 
        string newConsumerName, 
        long minIdleTimeMs = 60000)
    {
        try
        {
            
            //  Task<StreamPendingMessageInfo[]> StreamPendingMessagesAsync(RedisKey key, RedisValue groupName, int count, RedisValue consumerName, RedisValue? minId = null, RedisValue? maxId = null, CommandFlags flags = CommandFlags.None);
            var pendingInfo = await _database.StreamPendingMessagesAsync(_streamKey, consumerGroup, 100,new RedisValue());

            var oldMessages = pendingInfo
                .Where(p => p.IdleTimeInMilliseconds > minIdleTimeMs)
                .Select(p => p.MessageId)
                .ToArray();

            if (oldMessages.Length == 0)
                return Array.Empty<StreamEntry>();

            var claimedMessages = await _database.StreamClaimAsync(
                _streamKey, consumerGroup, newConsumerName, minIdleTimeMs, oldMessages);

            // _logger.LogInformation("Claimed {Count} messages from dead consumers for {ConsumerName}", 
            //     claimedMessages.Length, newConsumerName);

            return claimedMessages;
        }
        catch (Exception ex)
        {
           // _logger.LogError(ex, "Failed to claim pending messages for consumer {ConsumerName}", newConsumerName);
            return Array.Empty<StreamEntry>();
        }
    }

    /// <summary>
    /// Trim stream to keep only recent messages (memory management)
    /// </summary>
    private async Task<long> TrimStreamAsync(long maxLength = 1000000)
    {
        try
        {
            var trimmed = await _database.StreamTrimAsync(_streamKey, maxLength, useApproximateMaxLength: true);
            
            if (trimmed > 0)
            {
                // _logger.LogInformation("Trimmed {Count} messages from stream {StreamKey}, max length: {MaxLength}", 
                //     trimmed, _streamKey, maxLength);
            }

            return trimmed;
        }
        catch (Exception ex)
        {
            // _logger.LogError(ex, "Failed to trim stream {StreamKey}", _streamKey);
            return 0;
        }
    }

    /// <summary>
    /// Get stream information and statistics
    /// </summary>
    public async Task<StreamInfo> GetStreamInfoAsync()
    {
        try
        {
            return await _database.StreamInfoAsync(_streamKey);
        }
        catch (Exception ex)
        {
           // _logger.LogError(ex, "Failed to get stream info for {StreamKey}", _streamKey);
            throw;
        }
    }

    /// <summary>
    /// Delete messages by IDs (for cleanup)
    /// </summary>
    public async Task<long> DeleteMessagesAsync(params string[] messageIds)
    {
        if (messageIds == null || messageIds.Length == 0)
            return 0;

        try
        {
            var redisValues = messageIds.Select(id => (RedisValue)id).ToArray();
            var deleted = await _database.StreamDeleteAsync(_streamKey, redisValues);
            
           // _logger.LogTrace("Deleted {Count} messages from stream {StreamKey}", deleted, _streamKey);
            return deleted;
        }
        catch (Exception ex)
        {
            //_logger.LogError(ex, "Failed to delete messages from stream {StreamKey}", _streamKey);
            throw;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Ensure consumer group exists, create if not
    /// </summary>
    private async Task EnsureConsumerGroupExists(string consumerGroup)
    {
        if (!_config.ConsumerGroups.ContainsKey(consumerGroup))
        {
            //_logger.LogWarning("Consumer group {ConsumerGroup} not in configuration, creating with defaults", 
            //    consumerGroup);
        }

        await CreateConsumerGroupAsync(consumerGroup);
    }

    /// <summary>
    /// Trim stream if it exceeds configured maximum length
    /// </summary>
    private async Task TrimStreamIfNeeded()
    {
        try
        {
            var streamInfo = await _database.StreamInfoAsync(_streamKey);
            
            if (streamInfo.Length > _config.MaxStreamLength)
            {
                await TrimStreamAsync(_config.MaxStreamLength);
            }
        }
        catch (Exception ex)
        {
            //_logger.LogWarning(ex, "Failed to check/trim stream length");
        }
    }

    #endregion
}