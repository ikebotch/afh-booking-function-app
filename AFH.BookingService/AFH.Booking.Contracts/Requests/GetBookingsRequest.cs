namespace AFH.Booking.Contracts.Requests;

public sealed record GetBookingsRequest(
    string? AdviserId = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null
);
