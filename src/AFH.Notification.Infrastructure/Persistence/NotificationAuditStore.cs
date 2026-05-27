using AFH.Notification.Application.Abstractions;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Extensions.Logging;

namespace AFH.Notification.Infrastructure.Persistence;

public sealed class NotificationAuditStore : INotificationAuditStore
{
    private readonly ILogger<NotificationAuditStore> _logger;

    public NotificationAuditStore(ILogger<NotificationAuditStore> logger)
    {
        _logger = logger;
    }

    public Task RecordRequestedAsync(NotificationRequested notification, CancellationToken ct)
    {
        _logger.LogInformation(
            "Notification audit placeholder recorded. Type={NotificationType} CorrelationId={CorrelationId}",
            notification.Type,
            notification.CorrelationId);

        return Task.CompletedTask;
    }
}
