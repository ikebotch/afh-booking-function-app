namespace AFH.Booking.Domain.Options;

public sealed class LocationServiceOptions
{
    public const string SectionName = "LocationService";
    public string BaseUrl { get; set; } = default!;
    public string? FunctionKey { get; set; }
    public string? InternalToken { get; set; }
}
