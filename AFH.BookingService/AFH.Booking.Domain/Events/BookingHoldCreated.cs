namespace AFH.Booking.Domain.Events;

public sealed record BookingHoldCreated(
    string BookingId,
    string AdviserId,
    string CustomerId,
    DateTime StartUtc,
    DateTime EndUtc,
    string Timezone
);
