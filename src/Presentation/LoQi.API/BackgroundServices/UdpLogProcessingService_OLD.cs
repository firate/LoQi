using LoQi.Domain;
using System.Text.Json;
using LoQi.Application.DTOs;
using LoQi.Application.Services;

namespace LoQi.API.BackgroundServices;

public class UdpLogProcessingService_OLD : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IUdpPackageListener _udpPackageListener;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UdpLogProcessingService_OLD> _logger;

    private readonly List<LogEntry> _logBuffer = new();
    private readonly object _bufferLock = new();
    private Timer? _flushTimer;

    private int _batchSize;
    private int _flushIntervalMs;
    private int _maxMemoryMb;

    public UdpLogProcessingService_OLD(
        IServiceProvider serviceProvider,
        IUdpPackageListener udpPackageListener,
        IConfiguration configuration,
        ILogger<UdpLogProcessingService_OLD> logger
    )
    {
        _serviceProvider = serviceProvider;
        _udpPackageListener = udpPackageListener;
        _configuration = configuration;
        _logger = logger;
        
        // Load batch configuration with defaults
        _batchSize = _configuration.GetValue<int>("LoQi:Batching:BatchSize", 500);
        _flushIntervalMs = _configuration.GetValue<int>("LoQi:Batching:FlushIntervalMs", 1000);
        _maxMemoryMb = _configuration.GetValue<int>("LoQi:Batching:MaxMemoryMb", 100);

        _logger.LogInformation("UDP batch processing initialized - BatchSize: {BatchSize}, FlushInterval: {FlushInterval}ms, MaxMemory: {MaxMemory}MB",
            _batchSize, _flushIntervalMs, _maxMemoryMb);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var port = _configuration.GetValue<int>("UdpListener:Port", 10080);

        try
        {
            _logger.LogInformation("Starting UDP log processing service on port {Port}", port);

            // Start flush timer for periodic batch processing
            _flushTimer = new Timer(FlushBatchCallback, null, _flushIntervalMs, _flushIntervalMs);

            // UDP listener'ı başlat ve channel reader'ı al
            var messageReader = _udpPackageListener.StartAsync(port, stoppingToken);

            var processedCount = 0;
            var batchedCount = 0;
            var lastStatsTime = DateTime.UtcNow;

            // Channel'dan gelen string mesajları işle - FAST PATH
            await foreach (var rawMessage in messageReader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    // Convert to LogEntry - memory only operation
                    var logEntry = ConvertToLogEntry(rawMessage);
                    
                    // Add to batch buffer (fast in-memory operation)
                    var shouldFlush = AddToBatch(logEntry);
                    
                    processedCount++;
                    batchedCount++;

                    // Force flush if batch is full
                    if (shouldFlush)
                    {
                        // Non-blocking flush
                        _ = Task.Run(async () => await FlushBatch(), stoppingToken);
                    }

                    // Processing stats
                    if (DateTime.UtcNow - lastStatsTime > TimeSpan.FromMinutes(1))
                    {
                        _logger.LogInformation("Processed {Count} UDP messages, {Batched} in buffer", 
                            processedCount, GetBufferCount());
                        processedCount = 0;
                        lastStatsTime = DateTime.UtcNow;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process UDP message: {Message}",
                        rawMessage.Length > 100 ? rawMessage[..100] + "..." : rawMessage);
                    // Continue processing other messages
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("UDP log processing service was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UDP log processing service failed");
            throw; // Let host handle the failure
        }
        finally
        {
            // Final flush before shutdown
            await FlushBatch(force: true);
            
            _flushTimer?.Dispose();
            await _udpPackageListener.StopAsync();
            _logger.LogInformation("UDP log processing service stopped");
        }
    }
    
    /// <summary>
    /// Add log entry to batch buffer - returns true if batch should be flushed
    /// </summary>
    private bool AddToBatch(LogEntry logEntry)
    {
        lock (_bufferLock)
        {
            _logBuffer.Add(logEntry);
            
            // Check flush conditions
            var shouldFlushSize = _logBuffer.Count >= _batchSize;
            var shouldFlushMemory = CheckMemoryPressure();
            
            return shouldFlushSize || shouldFlushMemory;
        }
    }

    /// <summary>
    /// Check if memory usage is approaching limits
    /// </summary>
    private bool CheckMemoryPressure()
    {
        var workingSetMb = GC.GetTotalMemory(false) / 1024 / 1024;
        return workingSetMb > _maxMemoryMb;
    }

    /// <summary>
    /// Get current buffer count (thread-safe)
    /// </summary>
    private int GetBufferCount()
    {
        lock (_bufferLock)
        {
            return _logBuffer.Count;
        }
    }

    /// <summary>
    /// Timer callback for periodic batch flushing
    /// </summary>
    private async void FlushBatchCallback(object? state)
    {
        try
        {
            await FlushBatch();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in periodic batch flush");
        }
    }

    /// <summary>
    /// Flush current batch to database
    /// </summary>
    private async Task FlushBatch(bool force = false)
    {
        List<LogEntry> logsToFlush;
        
        lock (_bufferLock)
        {
            // Check if we should flush
            if (_logBuffer.Count == 0) 
                return;
                
            if (!force && _logBuffer.Count < _batchSize && !CheckMemoryPressure())
                return;

            // Take a snapshot of current buffer
            logsToFlush = new List<LogEntry>(_logBuffer);
            _logBuffer.Clear();
        }

        if (logsToFlush.Count == 0) 
            return;

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            await using var scope = _serviceProvider.CreateAsyncScope();
            var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
            
            // 🚀 BATCH INSERT - Single transaction!
            var success = await logService.AddLogsBatchAsync(logsToFlush);
            
            stopwatch.Stop();

            if (success)
            {
                _logger.LogTrace("Batch flush completed: {Count} logs in {Duration}ms", 
                    logsToFlush.Count, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning("Batch flush failed for {Count} logs", logsToFlush.Count);
                
                // TODO: Implement dead letter queue for failed batches
                await HandleFailedBatch(logsToFlush);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing batch of {Count} logs", logsToFlush.Count);
            
            // TODO: Implement retry logic or dead letter queue
            await HandleFailedBatch(logsToFlush);
        }
    }

    /// <summary>
    /// Handle failed batch - implement dead letter queue or retry logic
    /// </summary>
    private async Task HandleFailedBatch(List<LogEntry> failedLogs)
    {
        try
        {
            // Simple strategy: Save to file for manual recovery
            var deadLetterPath = Path.Combine("logs", "dead-letter");
            Directory.CreateDirectory(deadLetterPath);
            
            var fileName = $"failed-batch-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json";
            var filePath = Path.Combine(deadLetterPath, fileName);
            
            var json = System.Text.Json.JsonSerializer.Serialize(failedLogs, new JsonSerializerOptions 
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

    // private async Task ProcessRawMessage(string rawMessage)
    // {
    //     try
    //     {
    //         //  String mesajı LogEntry'ye çevir
    //         var logEntry = ConvertToLogEntry(rawMessage);
    //
    //         //  Scoped service kullanarak database'e kaydet
    //         await using var scope = _serviceProvider.CreateAsyncScope();
    //         var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
    //
    //         //  LogEntry'yi DTO'ya çevir ve kaydet
    //         var dto = new AddLogDto
    //         {
    //             Message = logEntry.Message,
    //             Level = logEntry.LevelId,
    //             Source = logEntry.Source
    //         };
    //
    //         await logService.AddLogAsync(dto);
    //     }
    //     catch (Exception ex)
    //     {
    //          _logger.LogError(ex, "Error processing raw message: {Message}", rawMessage);
    //         throw;
    //     }
    // }

    private LogEntry ConvertToLogEntry(string rawMessage)
    {
        try
        {
            //  JSON formatında mı kontrol et
            if (IsJsonFormat(rawMessage))
            {
                return ParseJsonLogEntry(rawMessage);
            }

            // ✅ Syslog formatında mı kontrol et
            // if (IsSyslogFormat(rawMessage))
            // {
            //     return ParseSyslogEntry(rawMessage);
            // }

            //  Plain text olarak işle
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

    // private static bool IsSyslogFormat(string message)
    // {
    //     // Syslog format: <priority>timestamp hostname tag: message
    //     return message.TrimStart().StartsWith('<') && message.Contains('>');
    // }

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
            // JSON parse başarısız - plain text olarak işle
            return ParsePlainTextEntry(jsonMessage);
        }
    }

    // private static LogEntry ParseSyslogEntry(string syslogMessage)
    // {
    //     try
    //     {
    //         // Basit syslog parsing: <priority>rest of message
    //         var priorityEnd = syslogMessage.IndexOf('>');
    //         if (priorityEnd > 0)
    //         {
    //             var priorityStr = syslogMessage[1..priorityEnd];
    //             var restMessage = syslogMessage[(priorityEnd + 1)..];
    //             
    //             if (int.TryParse(priorityStr, out var priority))
    //             {
    //                 var severity = priority % 8; // Syslog severity
    //                 var level = MapSyslogSeverityToLevel(severity);
    //                 
    //                 return new LogEntry
    //                 {
    //                     UniqueId = Guid.NewGuid(),
    //                     Message = restMessage.Trim(),
    //                     LevelId = level,
    //                     Source = "UDP:Syslog",
    //                     Timestamp = DateTime.UtcNow
    //                 };
    //             }
    //         }
    //
    //         // Syslog parse başarısız - plain text olarak işle
    //         return ParsePlainTextEntry(syslogMessage);
    //     }
    //     catch
    //     {
    //         return ParsePlainTextEntry(syslogMessage);
    //     }
    // }

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
            return 2; // Info default

        return levelStr.ToLowerInvariant() switch
        {
            "verbose" => 0,
            "debug" or "trace" => 1,
            "info" or "information" => 2,
            "warn" or "warning" => 3,
            "error" => 4,
            "fatal" or "critical" => 5,
            _ => 2 // Info default
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

        return 2; // Info default
    }

    // private static int MapSyslogSeverityToLevel(int syslogSeverity)
    // {
    //     return syslogSeverity switch
    //     {
    //         0 => 4, // Emergency -> Fatal
    //         1 => 4, // Alert -> Fatal
    //         2 => 4, // Critical -> Fatal
    //         3 => 3, // Error -> Error
    //         4 => 2, // Warning -> Warning
    //         5 => 1, // Notice -> Info
    //         6 => 1, // Informational -> Info
    //         7 => 0, // Debug -> Debug
    //         _ => 2  // Default -> Info
    //     };
    // }

    public override void Dispose()
    {
        _udpPackageListener?.Dispose();
        base.Dispose();
    }
}