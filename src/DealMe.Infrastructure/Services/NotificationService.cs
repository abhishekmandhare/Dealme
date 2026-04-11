namespace DealMe.Infrastructure.Services;

using DealMe.Core.DTOs;
using DealMe.Core.Interfaces.Notifications;
using Microsoft.Extensions.Logging;

public sealed class NotificationService : INotificationService
{
    private readonly AppriseClient _apprise;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(AppriseClient apprise, ILogger<NotificationService> logger)
    {
        _apprise = apprise;
        _logger = logger;
    }

    public async Task SendAsync(NotificationPayload payload, CancellationToken ct = default)
    {
        if (payload.AppriseUrls.Count == 0)
        {
            _logger.LogWarning("No Apprise URLs provided for notification: {Title}", payload.Title);
            return;
        }

        await _apprise.PostAsync(payload.Title, payload.Body, payload.AppriseUrls, ct);
    }
}
