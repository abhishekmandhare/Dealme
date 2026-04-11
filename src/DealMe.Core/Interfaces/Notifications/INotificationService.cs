namespace DealMe.Core.Interfaces.Notifications;

using DealMe.Core.DTOs;

public interface INotificationService
{
    Task SendAsync(NotificationPayload payload, CancellationToken ct = default);
}
