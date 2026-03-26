namespace AFH.Booking.Contracts.V1.Requests;

public sealed class ResolveDuplicateClientCaseRequest
{
    public string Resolution { get; init; } = default!;
    public string? ResolvedBy { get; init; }
    public string? Notes { get; init; }
}
