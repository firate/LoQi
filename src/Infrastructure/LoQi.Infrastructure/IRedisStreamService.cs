using LoQi.Application.DTOs;
using LoQi.Infrastructure.Models;
using StackExchange.Redis;

namespace LoQi.Infrastructure;

/// <summary>
/// Redis Stream service for LoQi log processing
/// Handles message queuing with consumer groups for different processing paths
/// </summary>
public interface IRedisStreamService
{
    /// <summary>
    /// Add a log message to the stream after processing attempt
    /// </summary>
    /// <param name="originalData">Raw UDP/TCP message</param>
    /// <param name="status">Processing status: success, failed, retry</param>
    /// <param name="parsedData">Parsed LogDto JSON (if successful)</param>
    /// <param name="errorInfo">Error details (if failed)</param>
    /// <param name="attempts">Retry attempt count</param>
    /// <returns>Stream entry ID</returns>
    Task<string> AddLogMessageAsync(
        string originalData, 
        LogProcessingStatus status, 
        string? parsedData = null, 
        string? errorInfo = null, 
        int attempts = 1);

    /// <summary>
    /// Read messages from a specific consumer group
    /// </summary>
    /// <param name="consumerGroup">Consumer group name (processed-logs, failed-logs, retry-logs)</param>
    /// <param name="consumerName">Consumer instance name</param>
    /// <param name="batchSize">Number of messages to read</param>
    /// <param name="blockTimeMs">Block time in milliseconds (0 = no blocking)</param>
    /// <returns>Stream entries</returns>
    Task<StreamEntry[]> ReadMessagesAsync(
        string consumerGroup, 
        string consumerName, 
        int batchSize = 100, 
        int blockTimeMs = 1000);

    /// <summary>
    /// Acknowledge processed messages
    /// </summary>
    /// <param name="consumerGroup">Consumer group name</param>
    /// <param name="messageIds">Message IDs to acknowledge</param>
    /// <returns>Number of acknowledged messages</returns>
    Task<long> AcknowledgeMessagesAsync(string consumerGroup, params string[] messageIds);

    /// <summary>
    /// Create consumer group if it doesn't exist
    /// </summary>
    /// <param name="consumerGroup">Consumer group name</param>
    /// <param name="startPosition">Starting position ($ for new messages, 0 for beginning)</param>
    /// <returns>True if created, false if already exists</returns>
    Task<bool> CreateConsumerGroupAsync(string consumerGroup, string startPosition = "$");

    /// <summary>
    /// Get pending message count for a consumer group
    /// </summary>
    /// <param name="consumerGroup">Consumer group name</param>
    /// <returns>Number of pending messages</returns>
    Task<long> GetPendingCountAsync(string consumerGroup);

    /// <summary>
    /// Get pending messages for a specific consumer (for recovery)
    /// </summary>
    /// <param name="consumerGroup">Consumer group name</param>
    /// <param name="consumerName">Consumer name</param>
    /// <returns>Pending stream entries</returns>
    Task<StreamEntry[]> GetPendingMessagesAsync(string consumerGroup, string consumerName);

    /// <summary>
    /// Claim pending messages from dead consumers
    /// </summary>
    /// <param name="consumerGroup">Consumer group name</param>
    /// <param name="newConsumerName">New consumer to claim messages</param>
    /// <param name="minIdleTimeMs">Minimum idle time before claiming</param>
    /// <returns>Claimed messages</returns>
    Task<StreamEntry[]> ClaimPendingMessagesAsync(
        string consumerGroup, 
        string newConsumerName, 
        long minIdleTimeMs = 60000);

    /// <summary>
    /// Trim stream to keep only recent messages (memory management)
    /// </summary>
    /// <param name="maxLength">Maximum number of messages to keep</param>
    /// <returns>Number of messages removed</returns>
    //Task<long> TrimStreamAsync(long maxLength = 1000000);

    /// <summary>
    /// Get stream information and statistics
    /// </summary>
    /// <returns>Stream info including length, consumer groups, etc.</returns>
    Task<StreamInfo> GetStreamInfoAsync();

    /// <summary>
    /// Delete messages by IDs (for cleanup)
    /// </summary>
    /// <param name="messageIds">Message IDs to delete</param>
    /// <returns>Number of deleted messages</returns>
    Task<long> DeleteMessagesAsync(params string[] messageIds);
}