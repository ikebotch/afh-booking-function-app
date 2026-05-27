namespace AFH.Notification.Application.Models;

public sealed record NotificationDeliveryResult(
    string Status,
    string? ProviderMessageId);
