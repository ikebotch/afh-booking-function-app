namespace AFH.Booking.Contracts.V1.Responses;

public sealed class RearrangeBookingResponse
{
    public string PreviousBookingId { get; init; } = default!;
    public string? PreviousBookingReference { get; init; }
    public string NewBookingId { get; init; } = default!;
    public string? NewBookingReference { get; init; }
    public string NewSlotId { get; init; } = default!;

    public string PreviousAdviserId { get; init; } = default!;
    public string PreviousAdviserName { get; init; } = default!;
    public DateTime PreviousStartUtc { get; init; }
    public DateTime PreviousEndUtc { get; init; }

    public string NewAdviserId { get; init; } = default!;
    public string NewAdviserName { get; init; } = default!;
    public DateTime NewStartUtc { get; init; }
    public DateTime NewEndUtc { get; init; }

    public string NotificationSummary { get; init; } = default!;
}
