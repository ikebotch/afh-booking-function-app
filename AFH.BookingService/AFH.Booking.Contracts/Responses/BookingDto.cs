namespace AFH.Booking.Contracts.Responses;

public sealed record BookingDto(
    string BookingId,
    string AdviserId,
    string CustomerId,
    DateTime StartUtc,
    DateTime EndUtc,
    string Timezone,
    string Status,
    MeetingMode Mode
);
