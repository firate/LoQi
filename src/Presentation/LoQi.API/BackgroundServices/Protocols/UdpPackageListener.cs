using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LoQi.Application.DTOs;
using LoQi.Infrastructure;
using LoQi.Infrastructure.Models;

namespace LoQi.API.BackgroundServices.Protocols;

public class UdpPackageListener : BackgroundService
{
    private readonly ILogger<UdpPackageListener> _logger;
    private UdpClient? _udpClient;
    private bool _isRunning;

    private readonly IConfiguration _configuration;
    private readonly IRedisStreamService _redisStreamService;
    private readonly int _port;

    public UdpPackageListener(ILogger<UdpPackageListener> logger, IRedisStreamService redisStreamService,
        IConfiguration configuration)
    {
        _logger = logger;
        _redisStreamService = redisStreamService;
        _configuration = configuration;
        _port = _configuration.GetValue<int>("UdpListener:Port", 10080);
    }

    private bool IsRunning => _isRunning;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _udpClient = new UdpClient(_port);

        //  OPTIMIZE UDP SOCKET BUFFERS
        _udpClient.Client.ReceiveBufferSize = 8 * 1024 * 1024; // 8MB receive buffer
        _udpClient.Client.SendBufferSize = 1024 * 1024; // 1MB send buffer

        _isRunning = true;

        _logger.LogInformation("UDP listener started on port {Port} and socket buffer {BufferSize}KB",
            _port,
            _udpClient.Client.ReceiveBufferSize / 1024);

        try
        {
            while (IsRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_udpClient == null) break;

                    var result = await _udpClient.ReceiveAsync(cancellationToken);
                    var message = Encoding.UTF8.GetString(result.Buffer);

                    // if can be converted AddLogDto, it will add as a Success, but need to string. 
                    var logEntry = ConvertToLogEntry(message);

                    if (logEntry is not null)
                    {
                        await _redisStreamService.AddLogMessageAsync(
                            originalData: message,
                            status: LogProcessingStatus.Success,
                            parsedData: JsonSerializer.Serialize(logEntry));

                        // iterate to next udp message
                        continue;
                    }

                    await _redisStreamService.AddLogMessageAsync(
                        originalData: message,
                        status: LogProcessingStatus.Failed,
                        parsedData: message);
                }
                catch (OperationCanceledException ex)
                {
                    _logger.LogInformation(ex, "UDP listener cancelled");
                    break;
                }
                catch (ObjectDisposedException ex)
                {
                    _logger.LogInformation(ex, "UDP client disposed");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error receiving UDP packet, retrying in 1 second...");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UDP listener crashed");
        }
    }

    private AddLogDto? ConvertToLogEntry(string rawMessage)
    {
        try
        {
            return IsJsonFormat(rawMessage) ? ParseJsonLogEntry(rawMessage) : ParsePlainTextEntry(rawMessage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse message format, treating as plain text: {Message}", rawMessage);
            return ParsePlainTextEntry(rawMessage);
        }
    }

    private static bool IsJsonFormat(string message)
    {
        try
        {
            // if can be converted, return directly true
            JsonDocument.Parse(message);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static AddLogDto? ParseJsonLogEntry(string jsonMessage)
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

            return new AddLogDto
            {
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

    private static AddLogDto? ParsePlainTextEntry(string plainMessage)
    {
        return new AddLogDto
        {
            Message = plainMessage.Trim(),
            LevelId = ParseLogLevelFromMessage(plainMessage),
            Source = "UDP:PlainText"
        };
    }

    private static int ParseLogLevelFromString(string? levelStr)
    {
        if (string.IsNullOrEmpty(levelStr))
        {
            return 2;
        }

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

        var level = lowerMessage switch
        {
            var x when x.Contains("fatal") || x.Contains("critical") => 5,
            var x when x.Contains("error") || x.Contains("exception") => 4,
            var x when x.Contains("warn") => 3,
            var x when x.Contains("info") => 2,
            var x when x.Contains("debug") || x.Contains("trace") => 1,
            _ => 2
        };

        return level;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!IsRunning) return;

        _logger.LogInformation("Stopping UDP listener...");

        _isRunning = false;
        _udpClient?.Close();

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        if (IsRunning)
        {
            _isRunning = false;
            _udpClient?.Close();
        }

        _udpClient?.Dispose();

        GC.SuppressFinalize(this);
    }
}