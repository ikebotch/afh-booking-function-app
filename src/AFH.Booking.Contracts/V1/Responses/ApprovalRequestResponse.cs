namespace AFH.Booking.Contracts.V1.Responses;

public sealed class ApprovalRequestResponse
{
    public string RequestId { get; init; } = default!;
    public string BookingId { get; init; } = default!;
    public string ChangeType { get; init; } = default!;
    public string RequestedBy { get; init; } = default!;
    public string Status { get; init; } = default!;
    public DateTime RequestedUtc { get; init; }
    public IReadOnlyList<string> RoutedTo { get; init; } = Array.Empty<string>();
    public string? ReasonCode { get; init; }
    public string? ReasonDetail { get; init; }
    public string? Reviewer { get; init; }
    public DateTime? ReviewedUtc { get; init; }
    public string? ReviewNotes { get; init; }
}
