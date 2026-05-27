using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Extensions.Logging;

namespace AFH.Notification.Application.Services;

public sealed class NotificationService : INotificationService, INotificationPublisher
{
    private readonly INotificationAuditStore _auditStore;
    private readonly INotificationRecipientResolver _recipientResolver;
    private readonly INotificationTemplateRenderer _templateRenderer;
    private readonly IReadOnlyList<INotificationDeliveryGateway> _deliveryGateways;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationAuditStore auditStore,
        INotificationRecipientResolver recipientResolver,
        INotificationTemplateRenderer templateRenderer,
        IEnumerable<INotificationDeliveryGateway> deliveryGateways,
        ILogger<NotificationService> logger)
    {
        _auditStore = auditStore;
        _recipientResolver = recipientResolver;
        _templateRenderer = templateRenderer;
        _deliveryGateways = deliveryGateways.ToArray();
        _logger = logger;
    }

    public async Task PublishAsync(NotificationRequested notification, CancellationToken ct)
    {
        var route = await _recipientResolver.ResolveAsync(notification, ct);
        var rendered = await _templateRenderer.RenderAsync(notification, ct);
        await _auditStore.RecordRequestedAsync(notification, ct);

        foreach (var content in rendered.ChannelContent)
        {
            var gateways = _deliveryGateways
                .Where(gateway => gateway.CanSend(content.Channel))
                .ToArray();

            if (gateways.Length == 0)
            {
                _logger.LogWarning(
                    "No notification delivery gateway registered. Type={NotificationType} CorrelationId={CorrelationId} Channel={NotificationChannel}",
                    notification.Type,
                    notification.CorrelationId,
                    content.Channel);
                continue;
            }

            foreach (var recipient in route.Recipients.Where(x => x.PreferredChannels?.Contains(content.Channel) == true))
            {
                var request = new NotificationDeliveryRequest(
                    notification.CorrelationId,
                    content.Channel,
                    recipient,
                    content.Subject,
                    content.HtmlBody,
                    content.TextBody,
                    new Dictionary<string, string>
                    {
                        ["sourceSystem"] = notification.SourceSystem,
                        ["notificationType"] = notification.Type.ToString(),
                        ["actorType"] = notification.Actor.ActorType,
                        ["actorSourceApplication"] = notification.Actor.SourceApplication
                    });

                foreach (var gateway in gateways)
                    await gateway.SendAsync(request, ct);
            }
        }

        _logger.LogInformation(
            "Notification request recorded. Type={NotificationType} CorrelationId={CorrelationId} RecipientCount={RecipientCount} CopyContactCentre={CopyContactCentre}",
            notification.Type,
            notification.CorrelationId,
            route.Recipients.Count,
            route.CopyContactCentre);
    }
}
