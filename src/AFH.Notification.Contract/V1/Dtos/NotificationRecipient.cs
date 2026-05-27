namespace AFH.Notification.Contract.V1.Dtos;

public sealed record NotificationRecipient(
    NotificationRecipientType Type,
    string? DisplayName,
    string? Email,
    string? MobileNumber = null,
    string? PushTarget = null,
    IReadOnlyList<NotificationChannel>? PreferredChannels = null);
