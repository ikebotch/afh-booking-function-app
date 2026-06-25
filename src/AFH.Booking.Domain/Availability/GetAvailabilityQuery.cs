using AFH.Booking.Domain.Location;

namespace AFH.Booking.Domain.Availability;

public sealed class GetAvailabilityQuery 
{
    public DateTime PreferredStart { get; set; }
    public double Duration { get; set; } = 0;
    public string MeetingType { get; set; } = "Review";
    public int Limit { get; set; } = 10;
    public string? Cursor { get; set; }

    public string? ClientId { get; init; }
    public string? TransactionId { get; init; }
    public string? ClientLookupRef { get; init; }
    public string? ClientLookupSource { get; init; }
    public string ProjectContext { get; init; } = "Booking";
    public bool IsRemote { get; init; }

    // Window support
    public DateTime? WindowStartUtc { get; init; }
    public DateTime? WindowEndUtc { get; init; }

    // Location / travel
    public string? RequestId { get; init; }
    public string? LocationRef { get; init; }           
    public LocationAddress? DestinationAddress { get; init; }

    public IReadOnlyList<string> PreferredAdviserIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Regions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RequiredSkills { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ExcludeAdviserIds { get; init; } = Array.Empty<string>();

    public int? SearchHorizonMinutes { get; init; } = 180;
    public int? MaxCandidates { get; init; } = 100;

    public int Take { get; init; } = 10; // number of slot start times per page
}



//public sealed class GetAvailabilityQuery
//{
//    public string TransactionId { get; set; } = default!;
//    public int DurationMinutes { get; set; }
//    public bool IsRemote { get; set; }

//    public PreferredStart PreferredStart { get; set; } = new();

//    public DateTime? WindowStartUtc { get; set; }
//    public DateTime? WindowEndUtc { get; set; }

//    public int Limit { get; set; } = 10;

//    public string? Cursor { get; set; }
//    public string? MeetingType { get; set; }
//}
