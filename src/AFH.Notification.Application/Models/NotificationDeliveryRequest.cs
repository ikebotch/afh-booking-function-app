using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Notification.Application.Models;

public sealed record NotificationDeliveryRequest(
    string CorrelationId,
    NotificationChannel Channel,
    NotificationRecipient Recipient,
    string? Subject,
    string? HtmlBody,
    string TextBody,
    IReadOnlyDictionary<string, string>? ProviderMetadata = null,
    IReadOnlyList<NotificationAttachment>? Attachments = null);
