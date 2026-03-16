namespace AFH.Booking.Contracts.V1.Requests;

public sealed class GetRearrangementOptionsRequest
{
    public string? PreferredStartUtc { get; init; }
    public AvailabilityWindowDto? Window { get; init; }
    public int? DurationMinutes { get; init; }
    public bool IncludeAlternativeAdvisers { get; init; } = true;
    public int Limit { get; init; } = 10;
}
