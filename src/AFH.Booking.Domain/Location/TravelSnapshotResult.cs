namespace AFH.Booking.Domain.Location;

public sealed class TravelSnapshotResult
{
    public string? SourceLocationRef { get; set; }
    public string? SourcePostcode { get; set; }
    public double? SourceLatitude { get; set; }
    public double? SourceLongitude { get; set; }
    public string? DestinationLocationRef { get; set; }
    public string? DestinationPostcode { get; set; }
    public double? DestinationLatitude { get; set; }
    public double? DestinationLongitude { get; set; }

    public int? TravelMinutes { get; set; }
    public double? DistanceMiles { get; set; }

    public string? Provider { get; set; }
    public string? Confidence { get; set; }

    public DateTime CalculatedUtc { get; set; }
}
