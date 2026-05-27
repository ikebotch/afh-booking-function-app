using AFH.Notification.Application.Abstractions;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Extensions.Logging;

namespace AFH.Notification.Application.Services;

public sealed class NotificationService : INotificationService, INotificationPublisher
{
    private readonly INotificationAuditStore _auditStore;
    private readonly INotificationRecipientResolver _recipientResolver;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationAuditStore auditStore,
        INotificationRecipientResolver recipientResolver,
        ILogger<NotificationService> logger)
    {
        _auditStore = auditStore;
        _recipientResolver = recipientResolver;
        _logger = logger;
    }

    public async Task PublishAsync(NotificationRequested notification, CancellationToken ct)
    {
        var route = await _recipientResolver.ResolveAsync(notification, ct);
        await _auditStore.RecordRequestedAsync(notification, ct);

        _logger.LogInformation(
            "Notification request recorded. Type={NotificationType} CorrelationId={CorrelationId} RecipientCount={RecipientCount} CopyContactCentre={CopyContactCentre}",
            notification.Type,
            notification.CorrelationId,
            route.Recipients.Count,
            route.CopyContactCentre);
    }
}
