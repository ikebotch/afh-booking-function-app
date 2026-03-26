namespace AFH.Booking.Contracts.V1.Responses;

public sealed class DownstreamUpdateReconciliationResponse
{
    public int RequestedCount { get; init; }
    public int RetriedCount { get; init; }
    public int SucceededCount { get; init; }
    public int FailedCount { get; init; }
    public IReadOnlyList<DownstreamUpdateReconciliationItemResponse> Results { get; init; } = [];
}

public sealed class DownstreamUpdateReconciliationItemResponse
{
    public string UpdateId { get; init; } = default!;
    public string BookingId { get; init; } = default!;
    public string ChangeType { get; init; } = default!;
    public string PreviousStatus { get; init; } = default!;
    public string CurrentStatus { get; init; } = default!;
    public int AttemptCount { get; init; }
    public DateTime? ProcessedUtc { get; init; }
    public string? ErrorMessage { get; init; }
}
