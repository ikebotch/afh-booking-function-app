namespace AFH.Booking.Domain.Events;

// Optional: only keep if you're doing domain event dispatching
public sealed record BookingConfirmed(
    string BookingId,
    string AdviserId,
    DateTime ConfirmedUtc
);
