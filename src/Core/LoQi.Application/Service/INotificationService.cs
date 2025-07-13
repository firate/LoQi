namespace LoQi.Application.Service;

public interface INotificationService
{
    Task SendNotification(object data);
}