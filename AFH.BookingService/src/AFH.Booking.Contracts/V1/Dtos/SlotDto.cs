using System.Text.Json.Serialization;

namespace AFH.Booking.Contracts.V1.Dtos;

public sealed class SlotDto
{
    public string SlotId { get; init; } = default!;
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }

    public int Rating { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, int>? ScoreBreakdown { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TravelMinutes { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? DistanceMiles { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TravelStatus { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TravelMessage { get; init; }

    // Hold visibility (optional but useful)
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HoldId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HoldStatus { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? HoldExpiresUtc { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HoldMessage { get; init; }
}