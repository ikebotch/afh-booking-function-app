namespace AFH.Booking.Contracts.V1.Responses;

public sealed class DownstreamUpdateResponse
{
    public string UpdateId { get; init; } = default!;
    public string BookingId { get; init; } = default!;
    public string ChangeType { get; init; } = default!;
    public string Status { get; init; } = default!;
    public DateTime CreatedUtc { get; init; }
    public DateTime? ProcessedUtc { get; init; }
    public string? ErrorMessage { get; init; }
}
