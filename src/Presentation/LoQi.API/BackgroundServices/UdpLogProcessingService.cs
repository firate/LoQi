using LoQi.Domain;
using System.Text.Json;
using LoQi.Application.DTOs;
using LoQi.Application.Services;

namespace LoQi.API.BackgroundServices;

public class UdpLogProcessingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IUdpPackageListener _udpPackageListener;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UdpLogProcessingService> _logger;

    public UdpLogProcessingService(
        IServiceProvider serviceProvider,
        IUdpPackageListener udpPackageListener,
        IConfiguration configuration,
        ILogger<UdpLogProcessingService> logger
    )
    {
        _serviceProvider = serviceProvider;
        _udpPackageListener = udpPackageListener;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var port = _configuration.GetValue<int>("UdpListener:Port", 10080);

        try
        {
            _logger.LogInformation("Starting UDP log processing service on port {Port}", port);

            // ✅ UDP listener'ı başlat ve channel reader'ı al
            var messageReader = _udpPackageListener.StartAsync(port, stoppingToken);

            var processedCount = 0;
            var lastStatsTime = DateTime.UtcNow;

            // ✅ Channel'dan gelen string mesajları işle
            await foreach (var rawMessage in messageReader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessRawMessage(rawMessage);
                    processedCount++;

                    // ✅ Processing stats
                    if (DateTime.UtcNow - lastStatsTime > TimeSpan.FromMinutes(1))
                    {
                        _logger.LogInformation("Processed {Count} UDP messages in last minute", processedCount);
                        processedCount = 0;
                        lastStatsTime = DateTime.UtcNow;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process UDP message: {Message}",
                        rawMessage.Length > 100 ? rawMessage[..100] + "..." : rawMessage);
                    // ✅ Continue processing other messages
                }
            }
        }
        catch (OperationCanceledException e)
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
            await _udpPackageListener.StopAsync();
             _logger.LogInformation("UDP log processing service stopped");
        }
    }

    private async Task ProcessRawMessage(string rawMessage)
    {
        try
        {
            // ✅ String mesajı LogEntry'ye çevir
            var logEntry = ConvertToLogEntry(rawMessage);

            // ✅ Scoped service kullanarak database'e kaydet
            await using var scope = _serviceProvider.CreateAsyncScope();
            var logService = scope.ServiceProvider.GetRequiredService<ILogService>();

            // ✅ LogEntry'yi DTO'ya çevir ve kaydet
            var dto = new AddLogDto
            {
                Message = logEntry.Message,
                Level = logEntry.LevelId,
                Source = logEntry.Source
            };

            await logService.AddLogAsync(dto);
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error processing raw message: {Message}", rawMessage);
            throw;
        }
    }

    private LogEntry ConvertToLogEntry(string rawMessage)
    {
        try
        {
            // ✅ JSON formatında mı kontrol et
            if (IsJsonFormat(rawMessage))
            {
                return ParseJsonLogEntry(rawMessage);
            }

            // ✅ Syslog formatında mı kontrol et
            // if (IsSyslogFormat(rawMessage))
            // {
            //     return ParseSyslogEntry(rawMessage);
            // }

            // ✅ Plain text olarak işle
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