using System.Threading.Channels;
using LoQi.API.Hubs;
using LoQi.API.Services;
using LoQi.Application.Services;

namespace LoQi.API.BackgroundServices;

public class BackgroundNotificationService : BackgroundService, IBackgroundNotificationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Channel<object> _channel;
    private readonly ChannelWriter<object> _writer;
    private readonly ChannelReader<object> _reader;
    private readonly ILogger<BackgroundNotificationService> _logger;
    private readonly IConfiguration _configuration;
    
    private volatile int _queueLength = 0;
    private volatile bool _isEnabled = true;

    public BackgroundNotificationService(
        IServiceProvider serviceProvider,
        ILogger<BackgroundNotificationService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;

        // Configuration'dan ayarları al
        var channelCapacity = _configuration.GetValue<int>("LoQi:Notifications:ChannelCapacity", 1000);
        _isEnabled = _configuration.GetValue<bool>("LoQi:Notifications:Enabled", true);

        var options = new BoundedChannelOptions(channelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest, // Eski notification'ları at
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };

        _channel = Channel.CreateBounded<object>(options);
        _writer = _channel.Writer;
        _reader = _channel.Reader;

        _logger.LogInformation("Background notification service initialized. Capacity: {Capacity}, Enabled: {Enabled}",
            channelCapacity, _isEnabled);
    }

    public bool IsEnabled => _isEnabled;
    public int GetQueueLength() => _queueLength;

    public void QueueNotification(object data)
    {
        if (!_isEnabled)
        {
            _logger.LogTrace("Notification service disabled, skipping notification");
            return;
        }

        // Live log listener yoksa notification gönderme!
        if (LogHub.GetActiveLiveLogListeners() == 0)
        {
            _logger.LogTrace("No active live log listeners, skipping notification");
            return;
        }

        if (_writer.TryWrite(data))
        {
            Interlocked.Increment(ref _queueLength);
            _logger.LogTrace("Notification queued. Queue length: {Length}", _queueLength);
        }
        else
        {
            _logger.LogWarning("Failed to queue notification. Channel might be full or closed.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background notification service started");

        try
        {
            await foreach (var data in _reader.ReadAllAsync(stoppingToken))
            {
                await ProcessNotification(data);
                Interlocked.Decrement(ref _queueLength);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Background notification service stopping gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background notification service encountered an error");
            throw; // Re-throw to restart the service
        }
        finally
        {
            _logger.LogInformation("Background notification service stopped");
        }
    }

    private async Task ProcessNotification(object data)
    {
        try
        {
            // Double-check: Hala listener var mı?
            if (LogHub.GetActiveLiveLogListeners() == 0)
            {
                _logger.LogTrace("No listeners during processing, skipping notification");
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            
            await notificationService.SendNotification(data);
            
            _logger.LogTrace("Notification sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send notification");
            // Don't rethrow - continue processing other notifications
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping background notification service...");
        
        // Signal no more writes
        _writer.Complete();
        
        await base.StopAsync(cancellationToken);
        
        _logger.LogInformation("Background notification service stopped gracefully");
    }
}