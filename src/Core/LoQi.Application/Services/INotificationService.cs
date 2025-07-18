namespace LoQi.Application.Services;

public interface INotificationService
{
    Task SendNotification(object data);
}