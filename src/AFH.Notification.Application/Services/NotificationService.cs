using AFH.Notification.Application.Abstractions;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Extensions.Logging;

namespace AFH.Notification.Application.Services;

public sealed class NotificationService : INotificationService, INotificationPublisher
{
    private readonly INotificationAuditStore _auditStore;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationAuditStore auditStore,
        ILogger<NotificationService> logger)
    {
        _auditStore = auditStore;
        _logger = logger;
    }

    public async Task PublishAsync(NotificationRequested notification, CancellationToken ct)
    {
        await _auditStore.RecordRequestedAsync(notification, ct);

        _logger.LogInformation(
            "Notification request recorded. Type={NotificationType} CorrelationId={CorrelationId}",
            notification.Type,
            notification.CorrelationId);
    }
}
