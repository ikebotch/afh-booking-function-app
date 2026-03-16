namespace AFH.Booking.Contracts.V1.Responses;

public sealed class GetRearrangementOptionsResponse
{
    public string BookingId { get; init; } = default!;
    public string CurrentAdviserId { get; init; } = default!;
    public string CurrentAdviserName { get; init; } = default!;
    public DateTime CurrentStartUtc { get; init; }
    public DateTime CurrentEndUtc { get; init; }
    public bool CurrentAdviserHasAvailability { get; init; }
    public bool RequiresAlternativeAdviserSelection { get; init; }
    public IReadOnlyList<ReasonOptionDto> RearrangementReasons { get; init; } = Array.Empty<ReasonOptionDto>();
    public IReadOnlyList<ReasonOptionDto> CancellationReasons { get; init; } = Array.Empty<ReasonOptionDto>();
    public IReadOnlyList<string> Considerations { get; init; } = Array.Empty<string>();
    public GetAvailabilityResponse Availability { get; init; } = new();
}

public sealed class ReasonOptionDto
{
    public string Code { get; init; } = default!;
    public string Label { get; init; } = default!;
}
