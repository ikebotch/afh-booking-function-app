namespace AFH.Notification.Contract.V1.Dtos;

public sealed record NotificationAttachment(
    string FileName,
    string ContentType,
    string Base64Content,
    string? ContentId = null,
    bool Inline = false,
    IReadOnlyList<string>? RecipientTypes = null,
    IReadOnlyList<NotificationChannel>? Channels = null);
