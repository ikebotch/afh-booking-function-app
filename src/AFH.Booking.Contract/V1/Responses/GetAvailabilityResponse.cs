using AFH.Booking.Contracts.V1.Common;
using AFH.Booking.Contracts.V1.Dtos;
using AFH.Booking.Contracts.V1.Dtos.Availability;
using System.Text.Json.Serialization;

namespace AFH.Booking.Contracts.V1.Responses;

public sealed class GetAvailabilityResponse
{
    //public string TransactionRef { get; set; } = default!;   // external LeadTech ref (TX-123 or clientId)
    public string TransactionId { get; set; } = default!;    // internal DB transaction id (Guid "N")

    public List<AdviserSlotsDto> Advisers { get; set; } = new();
    //public List<AvailabilityDayGroupDto>? Days { get; set; } = new();

    public PageResultDto<object> Paging { get; set; } = new(); // keep your existing paging shape if needed
}

public sealed class AvailabilityDayDto
{
    /// <summary>UTC date bucket (yyyy-MM-dd)</summary>
    public DateOnly DateUtc { get; set; }

    public List<AvailabilityAdviserDto> Advisers { get; set; } = new();
}

public sealed class AvailabilityAdviserDto
{
    public string AdviserId { get; set; } = default!;
    public string AdviserName { get; set; } = default!;

    //public IReadOnlyList<AdviserSlotsDto> Advisers { get; init; } = [];

   
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Region { get; set; }

    public List<SlotDto> Slots { get; set; } = new();
}

public sealed class AvailabilitySlotDto
{
    public string SlotId { get; set; } = default!;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }

    public int Score { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, int>? ScoreBreakdown { get; set; }

    // Travel summary
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TravelMinutes { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? DistanceMiles { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TravelStatus { get; set; } // NotRequested | Ok | Unavailable

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TravelMessage { get; set; }

    // Hold info (optional but super useful for UI)
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HoldId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HoldStatus { get; set; } // None | Active | Confirmed | Cancelled | Expired

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? HoldExpiresUtc { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HoldMessage { get; set; } 
}

