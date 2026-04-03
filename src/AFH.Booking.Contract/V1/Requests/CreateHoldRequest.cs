namespace AFH.Booking.Contracts.V1.Requests;

public sealed class CreateHoldRequest
{
    public string SlotId { get; init; } = default!;
    public string? TransactionId { get; init; }
}
