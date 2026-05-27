namespace AFH.Notification.Contract.V1.Dtos;

public sealed record NotificationRecipient(
    NotificationRecipientType Type,
    string? DisplayName,
    string? Email);
