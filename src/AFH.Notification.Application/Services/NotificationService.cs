using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Extensions.Logging;

namespace AFH.Notification.Application.Services;

public sealed class NotificationService : INotificationService, INotificationPublisher
{
    private readonly INotificationAuditStore _auditStore;
    private readonly INotificationDeliveryAuditStore _deliveryAuditStore;
    private readonly INotificationRecipientResolver _recipientResolver;
    private readonly INotificationTemplateRenderer _templateRenderer;
    private readonly IReadOnlyList<INotificationDeliveryGateway> _deliveryGateways;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationAuditStore auditStore,
        INotificationDeliveryAuditStore deliveryAuditStore,
        INotificationRecipientResolver recipientResolver,
        INotificationTemplateRenderer templateRenderer,
        IEnumerable<INotificationDeliveryGateway> deliveryGateways,
        ILogger<NotificationService> logger)
    {
        _auditStore = auditStore;
        _deliveryAuditStore = deliveryAuditStore;
        _recipientResolver = recipientResolver;
        _templateRenderer = templateRenderer;
        _deliveryGateways = deliveryGateways.ToArray();
        _logger = logger;
    }

    public Task PublishAsync(NotificationRequested notification, CancellationToken ct)
        => PublishAsync(notification, notificationOutboxId: null, ct);

    public async Task PublishAsync(NotificationRequested notification, Guid? notificationOutboxId, CancellationToken ct)
    {
        var route = await _recipientResolver.ResolveAsync(notification, ct);
        await _auditStore.RecordRequestedAsync(notification, ct);

        var sentTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var activeDeliveries = route.Recipients
            .SelectMany(recipient => recipient.PreferredChannels ?? Array.Empty<NotificationChannel>(), (recipient, channel) => new { Recipient = recipient, Channel = channel })
            .Where(x => x.Channel != NotificationChannel.Unknown)
            .ToList();

        foreach (var delivery in activeDeliveries)
        {
            if (!sentTargets.Add(GetTargetKey(delivery.Recipient, delivery.Channel)))
                continue;

            var gateways = _deliveryGateways
                .Where(gateway => gateway.CanSend(delivery.Channel))
                .ToArray();

            if (gateways.Length == 0)
            {
                _logger.LogWarning(
                    "No notification delivery gateway registered. Type={NotificationType} CorrelationId={CorrelationId} Channel={NotificationChannel}",
                    notification.Type,
                    notification.CorrelationId,
                    delivery.Channel);
                continue;
            }

            var recipientNotification = NotificationRecipientDataSafety.ForRecipientChannel(
                notification,
                delivery.Recipient,
                delivery.Channel);
            var rendered = await _templateRenderer.RenderAsync(recipientNotification, ct);
            var content = rendered.ChannelContent.SingleOrDefault(x => x.Channel == delivery.Channel);
            if (content is null)
                continue;

            var request = new NotificationDeliveryRequest(
                notification.CorrelationId,
                content.Channel,
                delivery.Recipient,
                content.Subject,
                content.HtmlBody,
                content.TextBody,
                new Dictionary<string, string>
                {
                    ["sourceSystem"] = notification.SourceSystem,
                    ["notificationType"] = notification.Type.Name,
                    ["actorType"] = notification.Actor.ActorType,
                    ["actorSourceApplication"] = notification.Actor.SourceApplication
                });

            foreach (var gateway in gateways)
            {
                var now = DateTime.UtcNow;
                try
                {
                    var result = await gateway.SendAsync(request, ct);
                    await _deliveryAuditStore.RecordAttemptAsync(
                        BuildAuditRecord(
                            recipientNotification,
                            notificationOutboxId,
                            content.Channel,
                            delivery.Recipient,
                            result.ProviderName ?? ResolveProviderName(gateway),
                            result.Status,
                            result.ProviderMessageId,
                            result.FailureDetails,
                            now,
                            content),
                        ct);
                }
                catch (Exception ex)
                {
                    await _deliveryAuditStore.RecordAttemptAsync(
                        BuildAuditRecord(
                            recipientNotification,
                            notificationOutboxId,
                            content.Channel,
                            delivery.Recipient,
                            ResolveProviderName(gateway),
                            "Failed",
                            null,
                            ex.Message,
                            now,
                            content),
                        ct);
                    throw;
                }
            }
        }

        _logger.LogInformation(
            "Notification request recorded. Type={NotificationType} CorrelationId={CorrelationId} RecipientCount={RecipientCount} CopyContactCentre={CopyContactCentre}",
            notification.Type,
            notification.CorrelationId,
            route.Recipients.Count,
            route.CopyContactCentre);
    }

    private static NotificationDeliveryAuditRecord BuildAuditRecord(
        NotificationRequested notification,
        Guid? notificationOutboxId,
        NotificationChannel channel,
        NotificationRecipient recipient,
        string providerName,
        string status,
        string? providerMessageId,
        string? failureDetails,
        DateTime now,
        NotificationChannelContent content)
    {
        var data = notification.Data;
        data.TryGetValue("bookingId", out var bookingId);
        data.TryGetValue("holdId", out var holdId);
        var sourceReferenceId = string.IsNullOrWhiteSpace(bookingId) ? holdId : bookingId;
        var sourceReferenceType = string.IsNullOrWhiteSpace(bookingId) && !string.IsNullOrWhiteSpace(holdId)
            ? "Hold"
            : !string.IsNullOrWhiteSpace(bookingId)
                ? "Booking"
                : null;
        var dispatchUid = Guid.NewGuid();
        var templateKey = ResolveTemplateDataValue(data, $"TemplateKey:{channel}") ??
                          ResolveTemplateDataValue(data, "TemplateKey") ??
                          $"{notification.SourceSystem}.{notification.Type.Name}";
        var templateVersion = ResolveTemplateDataValue(data, $"TemplateVersion:{channel}") ??
                              ResolveTemplateDataValue(data, "TemplateVersion") ??
                              "v1";
        var body = content.HtmlBody ?? content.TextBody;

        return new NotificationDeliveryAuditRecord(
            dispatchUid.ToString("N"),
            dispatchUid,
            notificationOutboxId,
            notification.SourceSystem,
            sourceReferenceType,
            sourceReferenceId,
            notification.Type.Name,
            channel.ToString(),
            recipient.RecipientType,
            recipient.Email,
            recipient.MobileNumber,
            providerName,
            status,
            providerMessageId,
            failureDetails,
            notification.CorrelationId,
            templateKey,
            templateVersion,
            now,
            DateTime.UtcNow,
            MessageLog: new NotificationMessageLogRecord(
                Guid.NewGuid(),
                dispatchUid,
                notificationOutboxId,
                notification.SourceSystem,
                notification.Type.Name,
                notification.CorrelationId,
                recipient.RecipientType,
                recipient.Email,
                recipient.MobileNumber,
                channel.ToString(),
                templateKey,
                templateVersion,
                TemplateContentId: null,
                content.Subject,
                body,
                content.ContentType,
                JsonSerializer.Serialize(data),
                ComputeSha256(body),
                now));
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string? ResolveTemplateDataValue(IReadOnlyDictionary<string, string> data, string key)
        => data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static string GetTargetKey(NotificationRecipient recipient, NotificationChannel channel)
        => channel switch
        {
            NotificationChannel.Email => $"{channel}:{recipient.Email?.Trim()}",
            NotificationChannel.Sms => $"{channel}:{recipient.MobileNumber?.Trim()}",
            NotificationChannel.Push => $"{channel}:{recipient.PushTarget?.Trim()}",
            _ => $"{channel}:"
        };

    private static string ResolveProviderName(INotificationDeliveryGateway gateway)
    {
        var typeName = gateway.GetType().Name;
        if (typeName.Contains("Graph", StringComparison.OrdinalIgnoreCase))
            return "Graph";
        if (typeName.Contains("Composed", StringComparison.OrdinalIgnoreCase))
            return "Composed";
        if (typeName.Contains("Email", StringComparison.OrdinalIgnoreCase))
            return "Email";
        return typeName;
    }
}
