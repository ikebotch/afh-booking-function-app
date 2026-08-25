namespace AFH.Booking.Application.Models.Notifications;

public enum BookingNotificationChannel
{
    Unknown = 0,
    Email = 1,
    Sms = 2,
    Push = 3
}

public sealed record BookingNotificationType(
    string SourceApplication,
    string Name)
{
    public override string ToString()
        => $"{SourceApplication}:{Name}";
}

public sealed record BookingNotificationActor(
    string ActorType,
    string SourceApplication,
    string? Id,
    string? DisplayName,
    string? Email);

public sealed record BookingNotificationRecipient(
    string RecipientType,
    string? DisplayName,
    string? Email,
    string? MobileNumber = null,
    string? PushTarget = null,
    IReadOnlyList<BookingNotificationChannel>? PreferredChannels = null);

public sealed record BookingNotificationRequest(
    BookingNotificationType Type,
    string CorrelationId,
    BookingNotificationActor Actor,
    IReadOnlyList<BookingNotificationRecipient> Recipients,
    IReadOnlyDictionary<string, string> Data,
    IReadOnlyList<BookingNotificationAttachment>? Attachments = null)
{
    public string SourceSystem => Type.SourceApplication;
}

public sealed record BookingNotificationAttachment(
    string FileName,
    string ContentType,
    string Base64Content,
    string? ContentId = null,
    bool Inline = false,
    IReadOnlyList<string>? RecipientTypes = null,
    IReadOnlyList<BookingNotificationChannel>? Channels = null);
