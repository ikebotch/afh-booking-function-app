namespace AFH.Booking.Contracts.V1.Responses;

public sealed class RecordNoShowResponse
{
    public string BookingId { get; init; } = default!;
    public string TransactionId { get; init; } = default!;
    public string LifecycleEventId { get; init; } = default!;
    public string PreviousState { get; init; } = default!;
    public string NewState { get; init; } = default!;
    public DateTime RecordedUtc { get; init; }
}
