namespace AFH.Booking.Contracts.V1.Requests;

public sealed class DownstreamUpdateReconciliationRequest
{
    public int? MaxCount { get; init; }
    public int? OlderThanMinutes { get; init; }
    public bool? IncludePending { get; init; }
}
