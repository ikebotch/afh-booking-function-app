using AFH.Booking.Application.Models.Lifecycle;

namespace AFH.Booking.Application.Abstractions.Lifecycle;

public interface IBookingLifecycleRecorder
{
    Task<string> RecordEventAsync(BookingLifecycleEventRecord entry, CancellationToken ct);
    Task RecordStepAsync(string lifecycleEventId, BookingLifecycleStepRecord step, CancellationToken ct);
}
