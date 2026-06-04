using AFH.Booking.Application.Models.Lifecycle;

namespace AFH.Booking.Application.Abstractions.Lifecycle;

public interface IBookingWorkflowNotificationAdapter
{
    Task<BookingWorkflowNotificationOutcome> RequestAsync(
        BookingWorkflowNotificationRequest request,
        CancellationToken ct);
}
