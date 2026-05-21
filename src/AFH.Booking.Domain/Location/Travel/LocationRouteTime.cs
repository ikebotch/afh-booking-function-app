namespace AFH.Booking.Domain.Location.Travel;

public sealed class LocationRouteTimeRequest
{
    public string? CorrelationId { get; set; }
    public DateTimeOffset DepartAt { get; set; }
    public LocationTravelCoordinates Source { get; set; } = new();
    public LocationTravelCoordinates Destination { get; set; } = new();
}

public sealed class LocationRouteTimeResult
{
    public string? CorrelationId { get; set; }
    public int? TravelTimeMinutes { get; set; }
    public double? TravelDistanceMiles { get; set; }
    public LocationRouteTimeStatus Status { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = [];
}

public enum LocationRouteTimeStatus
{
    Succeeded = 0,
    RouteUnavailable = 1,
    Failed = 2
}
