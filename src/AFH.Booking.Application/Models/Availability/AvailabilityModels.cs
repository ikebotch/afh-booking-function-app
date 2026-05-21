using AFH.Booking.Application.Models.Common;

namespace AFH.Booking.Application.Models.Availability;

public sealed class GetAvailabilityResponse
{
    public string TransactionId { get; set; } = default!;
    public List<AdviserSlotsDto> Advisers { get; set; } = new();
    public PageResult<object> Paging { get; set; } = new();
}

public sealed class AdviserSlotsDto
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public bool GoldStar { get; init; }
    public IReadOnlyList<SlotDto> Slots { get; init; } = [];
}

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

public sealed class AvailabilityDayGroupDto
{
    public DateOnly DateUtc { get; init; }
    public List<AvailabilityAdviserDto> Advisers { get; init; } = new();
    public int TotalSlots { get; init; }
    public int TotalAdvisers { get; init; }
    public List<AvailabilityWarningDto> Warnings { get; init; } = new();
}

public sealed class AvailabilityAdviserDto
{
    public string AdviserId { get; set; } = default!;
    public string AdviserName { get; set; } = default!;
    public string? Region { get; set; }
    public List<SlotDto> Slots { get; set; } = new();
}

public sealed class AvailabilityWarningDto
{
    public string Code { get; init; } = default!;
    public string Message { get; init; } = default!;
}

public sealed record AvailabilityRuleEvaluation(
    bool IsAllowed,
    bool WorkingPatternAllowed,
    bool CapacityAllowed,
    bool MinimumDurationAllowed,
    string? RejectionReason,
    IReadOnlyDictionary<string, int> Audit);
