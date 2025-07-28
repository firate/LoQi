using LoQi.Domain;
using System.Text.Json;
using System.Threading.Channels;
using LoQi.Application.Services;

namespace LoQi.API.BackgroundServices;

public class UdpLogsProcessingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IUdpPackageListener _udpPackageListener;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UdpLogsProcessingService> _logger;

    private readonly List<LogEntry> _logBuffer = new();
    private readonly object _bufferLock = new();

    private int _batchSize;
    private int _flushIntervalMs;
    private int _maxMemoryMb;
    private int _maxBufferSize;

    //  RESILIENCE STATE
    private int _consecutiveFailures = 0;
    private DateTime _lastSuccessTime = DateTime.UtcNow;
    private CircuitBreakerState _circuitState = CircuitBreakerState.Closed;
    private DateTime _circuitOpenTime = DateTime.MinValue;

    private readonly int _failureThreshold = 5;
    private readonly TimeSpan _circuitOpenDuration = TimeSpan.FromMinutes(2);
    private readonly TimeSpan _maxAllowedDowntime = TimeSpan.FromMinutes(5);

    public UdpLogsProcessingService(
        IServiceProvider serviceProvider,
        IUdpPackageListener udpPackageListener,
        IConfiguration configuration,
        ILogger<UdpLogsProcessingService> logger)
    {
        _serviceProvider = serviceProvider;
        _udpPackageListener = udpPackageListener;
        _configuration = configuration;
        _logger = logger;

        // Load batch configuration with defaults
        _batchSize = _configuration.GetValue<int>("LoQi:Batching:BatchSize", 500);
        _flushIntervalMs = _configuration.GetValue<int>("LoQi:Batching:FlushIntervalMs", 1000);
        _maxMemoryMb = _configuration.GetValue<int>("LoQi:Batching:MaxMemoryMb", 100);
        _maxBufferSize = _configuration.GetValue<int>("LoQi:Batching:MaxBufferSize", 50000);

        _logger.LogInformation(
            "UDP batch processing initialized - BatchSize: {BatchSize}, FlushInterval: {FlushInterval}ms, MaxMemory: {MaxMemory}MB, MaxBuffer: {MaxBuffer}",
            _batchSize, _flushIntervalMs, _maxMemoryMb, _maxBufferSize);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var port = _configuration.GetValue<int>("UdpListener:Port", 10080);

        try
        {
            _logger.LogInformation("Starting UDP log processing service on port {Port}", port);

            // Start periodic timers
            using var flushTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(_flushIntervalMs));
            using var monitorTimer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            
            var flushTask = RunFlushTimer(flushTimer, stoppingToken);
            var monitorTask = RunMonitorTimer(monitorTimer, stoppingToken);

            var messageReader = _udpPackageListener.StartAsync(port, stoppingToken);
            var udpTask = ProcessUdpMessages(messageReader, stoppingToken);
        
            //  Wait for all tasks to complete or first exception
            await Task.WhenAll(udpTask, flushTask, monitorTask);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("UDP log processing service was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UDP log processing service failed");
            throw;
        }
        finally
        {
            await FlushBatch(force: true);
            await _udpPackageListener.StopAsync();
            _logger.LogInformation("UDP log processing service stopped");
        }
    }
    
    private async Task ProcessUdpMessages(ChannelReader<string> messageReader, CancellationToken stoppingToken)
    {
        var processedCount = 0;
        var droppedCount = 0;
        var lastStatsTime = DateTime.UtcNow;

        await foreach (var rawMessage in messageReader.ReadAllAsync(stoppingToken))
        {
            try
            {
                var logEntry = ConvertToLogEntry(rawMessage);

                // CIRCUIT BREAKER CHECK
                if (_circuitState == CircuitBreakerState.Open)
                {
                    droppedCount++;
                    if (droppedCount % 1000 == 0)
                    {
                        _logger.LogWarning("Circuit breaker open - dropped {Count} messages", droppedCount);
                    }
                    continue;
                }

                var result = AddToBatch(logEntry);

                switch (result)
                {
                    case BatchResult.Added:
                        processedCount++;
                        break;
                        
                    case BatchResult.BufferFull:
                        droppedCount++;
                        //  Direct call instead of fire-and-forget Task.Run
                        await EmergencyFlush();
                        break;
                        
                    case BatchResult.ShouldFlush:
                        processedCount++;
                        //  Direct call instead of fire-and-forget Task.Run
                        await FlushBatch();
                        break;
                }

                // Processing stats
                if (DateTime.UtcNow - lastStatsTime > TimeSpan.FromMinutes(1))
                {
                    _logger.LogInformation(
                        "Processed {Processed} UDP messages, {Dropped} dropped, {Buffer} in buffer, Circuit: {Circuit}",
                        processedCount, droppedCount, GetBufferCount(), _circuitState);
                    processedCount = 0;
                    droppedCount = 0;
                    lastStatsTime = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process UDP message: {Message}",
                    rawMessage.Length > 100 ? rawMessage[..100] + "..." : rawMessage);
            }
        }
    }
    
    private async Task RunFlushTimer(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await FlushBatch();
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown - normal
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Flush timer failed");
        }
    }
    
    private async Task RunMonitorTimer(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await MonitorSystemHealth();
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown - normal
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Monitor timer failed");
        }
    }

    /// <summary>
    /// Add log entry to batch buffer with resilience checks
    /// </summary>
    private BatchResult AddToBatch(LogEntry logEntry)
    {
        lock (_bufferLock)
        {
            // CRITICAL: Check if buffer is at max capacity
            if (_logBuffer.Count >= _maxBufferSize)
            {
                _logger.LogWarning("Buffer at maximum capacity ({MaxSize}), dropping message", _maxBufferSize);
                return BatchResult.BufferFull;
            }

            _logBuffer.Add(logEntry);

            // Check flush conditions
            var shouldFlushSize = _logBuffer.Count >= _batchSize;
            var shouldFlushMemory = CheckMemoryPressure();

            if (shouldFlushSize || shouldFlushMemory)
            {
                return BatchResult.ShouldFlush;
            }

            return BatchResult.Added;
        }
    }

    /// <summary>
    ///  Monitor system health and circuit breaker state
    /// </summary>
    private async Task MonitorSystemHealth()
    {
        try
        {
            var bufferCount = GetBufferCount();
            var bufferUsage = (double)bufferCount / _maxBufferSize * 100;

            //  BUFFER MONITORING
            if (bufferUsage > 90)
            {
                _logger.LogCritical("Buffer critically full: {Usage:F1}% ({Count}/{MaxSize})",
                    bufferUsage, bufferCount, _maxBufferSize);

                await EmergencyFlush();
            }
            else if (bufferUsage > 70)
            {
                _logger.LogWarning("Buffer filling up: {Usage:F1}% ({Count}/{MaxSize})",
                    bufferUsage, bufferCount, _maxBufferSize);
            }

            // 🔌 CIRCUIT BREAKER MONITORING
            if (_circuitState == CircuitBreakerState.Open)
            {
                if (DateTime.UtcNow - _circuitOpenTime > _circuitOpenDuration)
                {
                    _circuitState = CircuitBreakerState.HalfOpen;
                    _logger.LogInformation("Circuit breaker moving to half-open state");
                }
            }

            //  EXTENDED DOWNTIME CHECK
            var downtime = DateTime.UtcNow - _lastSuccessTime;
            if (downtime > _maxAllowedDowntime && _circuitState != CircuitBreakerState.Open)
            {
                _logger.LogCritical("System down for {Downtime}, opening circuit breaker", downtime);
                _circuitState = CircuitBreakerState.Open;
                _circuitOpenTime = DateTime.UtcNow;

                await EmergencyFlush();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in health monitoring");
        }
    }

    /// <summary>
    ///  Emergency flush - drain buffer to prevent memory overflow
    /// </summary>
    private async Task EmergencyFlush()
    {
        List<LogEntry> logsToFlush;

        lock (_bufferLock)
        {
            if (_logBuffer.Count == 0) return;

            // Take everything and clear buffer
            logsToFlush = new List<LogEntry>(_logBuffer);
            _logBuffer.Clear();
        }

        _logger.LogWarning("Emergency flush initiated for {Count} logs", logsToFlush.Count);

        // Try to save, but don't fail if database is down
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var logService = scope.ServiceProvider.GetRequiredService<ILogService>();

            await logService.AddLogsBatchAsync(logsToFlush);
            _logger.LogInformation("Emergency flush completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Emergency flush failed, saving to dead letter");
            await HandleFailedBatch(logsToFlush);
        }
    }

    /// <summary>
    /// Flush current batch to database with resilience
    /// </summary>
    private async Task FlushBatch(bool force = false)
    {
        List<LogEntry> logsToFlush;

        lock (_bufferLock)
        {
            if (_logBuffer.Count == 0) return;

            if (!force && _logBuffer.Count < _batchSize && !CheckMemoryPressure())
                return;

            logsToFlush = new List<LogEntry>(_logBuffer);
            _logBuffer.Clear();
        }

        if (logsToFlush.Count == 0) return;

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            await using var scope = _serviceProvider.CreateAsyncScope();
            var logService = scope.ServiceProvider.GetRequiredService<ILogService>();

            var success = await logService.AddLogsBatchAsync(logsToFlush);

            stopwatch.Stop();

            if (success)
            {
                // SUCCESS - Reset resilience counters
                if (_consecutiveFailures > 0)
                {
                    _logger.LogInformation("Database recovered after {Failures} failures", _consecutiveFailures);
                }

                _consecutiveFailures = 0;
                _lastSuccessTime = DateTime.UtcNow;

                if (_circuitState == CircuitBreakerState.HalfOpen)
                {
                    _circuitState = CircuitBreakerState.Closed;
                    _logger.LogInformation("Circuit breaker closed - database recovered");
                }

                _logger.LogTrace("Batch flush completed: {Count} logs in {Duration}ms",
                    logsToFlush.Count, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                await HandleFlushFailure(logsToFlush, "Batch flush returned false");
            }
        }
        catch (Exception ex)
        {
            await HandleFlushFailure(logsToFlush, ex.Message, ex);
        }
    }

    /// <summary>
    ///  Handle flush failure with circuit breaker logic
    /// </summary>
    private async Task HandleFlushFailure(List<LogEntry> failedLogs, string reason, Exception? ex = null)
    {
        _consecutiveFailures++;

        if (ex != null)
        {
            _logger.LogError(ex, "Database flush failed {Count} times: {Reason}", _consecutiveFailures, reason);
        }
        else
        {
            _logger.LogWarning("Database flush failed {Count} times: {Reason}", _consecutiveFailures, reason);
        }

        //  CIRCUIT BREAKER LOGIC
        if (_consecutiveFailures >= _failureThreshold)
        {
            _circuitState = CircuitBreakerState.Open;
            _circuitOpenTime = DateTime.UtcNow;

            _logger.LogCritical("Circuit breaker opened due to {Count} consecutive failures", _consecutiveFailures);
        }

        await HandleFailedBatch(failedLogs);
    }

    private bool CheckMemoryPressure()
    {
        var workingSetMb = GC.GetTotalMemory(false) / 1024 / 1024;
        return workingSetMb > _maxMemoryMb;
    }

    private int GetBufferCount()
    {
        lock (_bufferLock)
        {
            return _logBuffer.Count;
        }
    }
    
    private async Task HandleFailedBatch(List<LogEntry> failedLogs)
    {
        try
        {
            var deadLetterPath = Path.Combine("logs", "dead-letter");
            Directory.CreateDirectory(deadLetterPath);

            var fileName = $"failed-batch-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json";
            var filePath = Path.Combine(deadLetterPath, fileName);

            var json = JsonSerializer.Serialize(failedLogs, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json);

            _logger.LogWarning("Saved {Count} failed logs to dead letter file: {File}",
                failedLogs.Count, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save dead letter logs");
        }
    }

    private LogEntry ConvertToLogEntry(string rawMessage)
    {
        try
        {
            if (IsJsonFormat(rawMessage))
            {
                return ParseJsonLogEntry(rawMessage);
            }

            return ParsePlainTextEntry(rawMessage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse message format, treating as plain text: {Message}", rawMessage);
            return ParsePlainTextEntry(rawMessage);
        }
    }

    private static bool IsJsonFormat(string message)
    {
        var trimmed = message.Trim();
        return trimmed.StartsWith('{') && trimmed.EndsWith('}');
    }

    private static LogEntry ParseJsonLogEntry(string jsonMessage)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonMessage);
            var root = document.RootElement;

            var message = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : jsonMessage;
            var level = root.TryGetProperty("level", out var levelProp)
                ? ParseLogLevelFromString(levelProp.GetString())
                : 2;
            var source = root.TryGetProperty("source", out var sourceProp) ? sourceProp.GetString() : "UDP:JSON";

            return new LogEntry
            {
                UniqueId = Guid.NewGuid(),
                Message = message ?? jsonMessage,
                LevelId = level,
                Source = source ?? "UDP:JSON"
            };
        }
        catch
        {
            return ParsePlainTextEntry(jsonMessage);
        }
    }

    private static LogEntry ParsePlainTextEntry(string plainMessage)
    {
        return new LogEntry
        {
            UniqueId = Guid.NewGuid(),
            Message = plainMessage.Trim(),
            LevelId = ParseLogLevelFromMessage(plainMessage),
            Source = "UDP:PlainText",
            Timestamp = DateTime.UtcNow
        };
    }

    private static int ParseLogLevelFromString(string? levelStr)
    {
        if (string.IsNullOrEmpty(levelStr))
            return 2;

        return levelStr.ToLowerInvariant() switch
        {
            "verbose" => 0,
            "debug" or "trace" => 1,
            "info" or "information" => 2,
            "warn" or "warning" => 3,
            "error" => 4,
            "fatal" or "critical" => 5,
            _ => 2  // Info default
        };
    }

    private static int ParseLogLevelFromMessage(string message)
    {
        var lowerMessage = message.ToLowerInvariant();

        if (lowerMessage.Contains("fatal") || lowerMessage.Contains("critical"))
            return 5;
        if (lowerMessage.Contains("error") || lowerMessage.Contains("exception"))
            return 4;
        if (lowerMessage.Contains("warn"))
            return 3;
        if (lowerMessage.Contains("info"))
            return 2;
        if (lowerMessage.Contains("debug") || lowerMessage.Contains("trace"))
            return 1;
        if (lowerMessage.Contains("verbose"))
            return 0;

        return 2;
    }

    public override void Dispose()
    {
        // PeriodicTimer'lar using ile otomatik dispose ediliyor
        _udpPackageListener?.Dispose();
        base.Dispose();
    }

    private enum BatchResult
    {
        Added,
        ShouldFlush,
        BufferFull
    }

    private enum CircuitBreakerState
    {
        Closed,   // Normal operation
        Open,     // Failing, dropping messages
        HalfOpen  // Testing if service recovered
    }
}