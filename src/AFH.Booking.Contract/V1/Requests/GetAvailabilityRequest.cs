using System.Text.Json.Serialization;

namespace AFH.Booking.Contracts.V1.Requests;

public sealed class GetAvailabilityRequest
{
    public string? ClientId { get; init; }
    public string? TransactionId { get; init; }
    public string? PreferredStartUtc { get; init; }
    public AvailabilityWindowDto? Window{ get; init; }


    public int Duration { get; init; }
    public bool IsRemote { get; init; }
    public string MeetingType { get; init; } = "Initial";



    public TravelMatrixAddress? DestinationAddress { get; init; }


    public IReadOnlyList<string> PreferredAdviserIds { get; init; } = Array.Empty<string>();

     public IReadOnlyList<string> Regions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RequiredSkills { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ExcludeAdviserIds { get; init; } = Array.Empty<string>();


    public int? SearchHorizonMinutes { get; init; } = 180;
    public int MaxCandidates { get; init; } = 100;


    public int Limit { get; init; } = 10;
    public string? Cursor { get; init; }
}


public sealed class AvailabilityWindowDto
{
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
}


public sealed class TravelMatrixAddress
{
    public string Line1 { get; set; } = default!;

    public string Town { get; set; } = default!;

    public string Postcode { get; set; } = default!;

    public string Country { get; set; } = "UK";
}
