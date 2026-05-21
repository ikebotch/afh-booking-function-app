namespace AFH.Booking.Application.Models.Availability;

public sealed class SlotDto
{
    public string SlotId { get; init; } = default!;
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public int Rating { get; init; }
    public IReadOnlyDictionary<string, int>? ScoreBreakdown { get; init; }
    public int? TravelMinutes { get; init; }
    public int? CompanyBufferMinutes { get; init; }
    public decimal? DistanceMiles { get; init; }
    public string? TravelStatus { get; init; }
    public string? TravelMessage { get; init; }
    public string? HoldId { get; init; }
    public string? HoldStatus { get; init; }
    public DateTime? HoldExpiresUtc { get; init; }
    public string? HoldMessage { get; init; }
}
