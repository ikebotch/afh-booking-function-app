using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Notification.Contract.V1.Requests;

public sealed record NotificationRequested(
    NotificationType Type,
    string CorrelationId,
    NotificationActor Actor,
    IReadOnlyList<NotificationRecipient> Recipients,
    IReadOnlyDictionary<string, string> Data,
    IReadOnlyList<NotificationAttachment>? Attachments = null)
{
    public string SourceSystem => Type.SourceApplication;
}
