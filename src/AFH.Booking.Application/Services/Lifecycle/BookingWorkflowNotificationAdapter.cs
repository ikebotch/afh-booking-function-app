using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Models.Lifecycle;
using AFH.Booking.Application.Models.Lifecycle.Constants;
using AFH.Booking.Application.Models.Notifications;

namespace AFH.Booking.Application.Services.Lifecycle;

public sealed class BookingWorkflowNotificationAdapter : IBookingWorkflowNotificationAdapter
{
    private readonly IBookingNotificationStep _notificationStep;

    public BookingWorkflowNotificationAdapter(IBookingNotificationStep notificationStep)
    {
        _notificationStep = notificationStep;
    }

    public async Task<BookingWorkflowNotificationOutcome> RequestAsync(
        BookingWorkflowNotificationRequest request,
        CancellationToken ct)
    {
        var result = await _notificationStep.ExecuteAsync(
            request.LifecycleEventType,
            request.CorrelationId,
            request.ActorType,
            request.Recipients,
            request.Data,
            ct);

        return BookingWorkflowNotificationOutcome.FromStepResult(
            MapNotificationTypeName(request.LifecycleEventType),
            request.Recipients.Count,
            null,
            result.Status,
            result.ErrorCode,
            result.ErrorDetails);
    }

    private static string MapNotificationTypeName(string lifecycleEventType) => lifecycleEventType switch
    {
        LifecycleEventTypes.Booked => BookingNotificationTypes.BookingConfirmed.Name,
        LifecycleEventTypes.Cancelled => BookingNotificationTypes.BookingCancelled.Name,
        LifecycleEventTypes.Rearranged => BookingNotificationTypes.BookingRescheduled.Name,
        LifecycleEventTypes.HoldCreated => BookingNotificationTypes.BookingHoldCreated.Name,
        _ => BookingNotificationTypes.TryGetByName(lifecycleEventType)?.Name ?? lifecycleEventType
    };
}
