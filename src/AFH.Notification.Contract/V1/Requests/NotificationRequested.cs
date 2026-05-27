using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Notification.Contract.V1.Requests;

public sealed record NotificationRequested(
    NotificationType Type,
    string SourceSystem,
    string CorrelationId,
    NotificationActor Actor,
    IReadOnlyList<NotificationRecipient> Recipients,
    IReadOnlyDictionary<string, string> Data)
{
    public static NotificationRequested BookingConfirmed(
        string bookingId,
        NotificationActor actor,
        IReadOnlyList<NotificationRecipient> recipients,
        IReadOnlyDictionary<string, string> data)
        => new(NotificationType.BookingConfirmed, "Booking", bookingId, actor, recipients, data);

    public static NotificationRequested BookingRescheduled(
        string bookingId,
        NotificationActor actor,
        IReadOnlyList<NotificationRecipient> recipients,
        IReadOnlyDictionary<string, string> data)
        => new(NotificationType.BookingRescheduled, "Booking", bookingId, actor, recipients, data);

    public static NotificationRequested BookingCancelled(
        string bookingId,
        NotificationActor actor,
        IReadOnlyList<NotificationRecipient> recipients,
        IReadOnlyDictionary<string, string> data)
        => new(NotificationType.BookingCancelled, "Booking", bookingId, actor, recipients, data);

    public static NotificationRequested BookingHoldCreated(
        string bookingId,
        NotificationActor actor,
        IReadOnlyList<NotificationRecipient> recipients,
        IReadOnlyDictionary<string, string> data)
        => new(NotificationType.BookingHoldCreated, "Booking", bookingId, actor, recipients, data);
}
