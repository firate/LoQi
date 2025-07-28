namespace LoQi.Application.Services;

public interface IBackgroundNotificationService
{
    void QueueNotification(object data);
    int GetQueueLength();
    bool IsEnabled { get; }
}