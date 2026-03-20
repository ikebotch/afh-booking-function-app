namespace AFH.Booking.Domain.Options;

public sealed class LocationServiceOptions
{
    public const string SectionName = "LocationService";
    public string BaseUrl { get; set; } = default!;
    public string MasterKey { get; set; } = default!; // location-function-v2-master-key
}
