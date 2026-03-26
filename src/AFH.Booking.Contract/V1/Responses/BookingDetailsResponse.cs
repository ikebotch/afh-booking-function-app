namespace AFH.Booking.Contracts.V1.Responses;

public sealed class BookingDetailsResponse
{
    public string BookingId { get; init; } = default!;
    public string SlotId { get; init; } = default!;
    public string TransactionId { get; init; } = default!;
    public string TransactionRef { get; init; } = default!;

    public string AdviserId { get; init; } = default!;
    public string AdviserName { get; init; } = default!;

    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public int DurationMinutes { get; init; }

    public bool IsRemote { get; init; }
    public string? MeetingType { get; init; }

    public string Status { get; init; } = default!;
    public DateTime? ConfirmedUtc { get; init; }
    public DateTime? CancelledUtc { get; init; }
    public string? CancelReason { get; init; }
}
