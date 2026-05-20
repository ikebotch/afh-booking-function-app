namespace AFH.Booking.Domain.Location.Travel;

public sealed class LocationTravelCoverageRequest
{
    public string SourcePostcode { get; set; } = string.Empty;
    public DateTimeOffset? RequestedDepartureTime { get; set; }
    public LocationTravelTimingMode TimingMode { get; set; } = LocationTravelTimingMode.TimeIndependent;
    public string? AppointmentType { get; set; }
    public string? Channel { get; set; }
    public string? CorrelationId { get; set; }
    public string? RequestedBy { get; set; }
    public List<LocationTravelCoverageDestination> Destinations { get; set; } = new();
}

public sealed class LocationTravelCoverageDestination
{
    public string CorrelationId { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public int? MaxTravelTimeMinutes { get; set; }
    public double? MaxDistanceMiles { get; set; }
}

public enum LocationTravelTimingMode
{
    TimeIndependent = 0,
    DepartureTime = 1
}

public sealed class LocationTravelCoverageResult
{
    public string SourcePostcode { get; set; } = string.Empty;
    public LocationTravelCoordinates? SourceCoordinates { get; set; }
    public List<LocationTravelCoverageOutcome> Destinations { get; set; } = new();
}

public sealed class LocationTravelCoverageOutcome
{
    public string CorrelationId { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public LocationTravelCoverageStatus Status { get; set; }
    public LocationTravelCoordinates? Coordinates { get; set; }
    public LocationTravelRouteOutcome? Route { get; set; }
    public LocationCoverageOutcome? Coverage { get; set; }
}

public enum LocationTravelCoverageStatus
{
    Succeeded = 0,
    SourcePostcodeUnresolved = 1,
    DestinationPostcodeUnresolved = 2,
    RouteUnavailable = 3,
    Failed = 4
}

public sealed class LocationTravelRouteOutcome
{
    public int TravelTimeMinutes { get; set; }
    public double TravelDistanceMiles { get; set; }
    public string Confidence { get; set; } = string.Empty;
    public LocationTravelResolutionSource ResolutionSource { get; set; }
}

public enum LocationTravelResolutionSource
{
    Unknown = 0,
    Cache = 1,
    Database = 2,
    AzureMaps = 3
}

public sealed class LocationCoverageOutcome
{
    public bool IsWithinCoverage { get; set; }
    public int? MaxTravelTimeMinutes { get; set; }
    public double? MaxDistanceMiles { get; set; }
}

public sealed class LocationTravelCoordinates
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
