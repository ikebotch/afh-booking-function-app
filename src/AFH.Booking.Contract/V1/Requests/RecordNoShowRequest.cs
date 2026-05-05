namespace AFH.Booking.Contracts.V1.Requests;

public sealed class RecordNoShowRequest
{
    public string? RequestedBy { get; init; }
    public string? ActorId { get; init; }
    public string? ReasonCode { get; init; }
    public string? ReasonDetail { get; init; }
}
