using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Notification.Application.Models;

public sealed record NotificationDeliveryRequest(
    string CorrelationId,
    NotificationRecipient Recipient,
    string Subject,
    string HtmlBody,
    string TextBody);
