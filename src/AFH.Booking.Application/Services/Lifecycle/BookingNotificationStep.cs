using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Models.Lifecycle.Constants;
using AFH.Booking.Application.Models.Notifications;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Booking.Application.Services.Lifecycle;

public sealed class BookingNotificationStep : IBookingNotificationStep
{
    private readonly INotificationPublisher _publisher;

    public BookingNotificationStep(INotificationPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task<(string Status, string? ErrorCode, string? ErrorDetails)> ExecuteAsync(
        string lifecycleEventType,
        string correlationId,
        string actorType,
        IReadOnlyList<NotificationRecipient> recipients,
        IReadOnlyDictionary<string, string> data,
        CancellationToken ct)
    {
        var notificationType = MapEventType(lifecycleEventType);
        if (notificationType is null)
            return (LifecycleStepStatuses.Skipped, null, null);

        try
        {
            await _publisher.PublishAsync(
                new NotificationRequested(
                    notificationType,
                    correlationId,
                    new NotificationActor(actorType, "Booking", null, null, null),
                    recipients,
                    data.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)),
                ct);

            return (LifecycleStepStatuses.Succeeded, null, null);
        }
        catch (Exception ex)
        {
            return (LifecycleStepStatuses.Failed, LifecycleErrorCodes.NotificationFailed, ex.Message);
        }
    }

    private static NotificationType? MapEventType(string lifecycleEventType) => lifecycleEventType switch
    {
        LifecycleEventTypes.Booked => BookingNotificationTypes.BookingConfirmed,
        LifecycleEventTypes.Cancelled => BookingNotificationTypes.BookingCancelled,
        LifecycleEventTypes.Rearranged => BookingNotificationTypes.BookingRescheduled,
        LifecycleEventTypes.HoldCreated => BookingNotificationTypes.BookingHoldCreated,
        _ => null
    };
}
