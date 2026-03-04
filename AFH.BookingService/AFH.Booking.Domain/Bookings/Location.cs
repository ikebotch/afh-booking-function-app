namespace AFH.Booking.Domain.Bookings;

public sealed class Location
{
    public Location() { }

    public string? DisplayName { get;  set; }
    public string? AddressLine1 { get; set; }
    public string? City { get; set; }
    public string? Postcode { get; set; }

    public string? OnlineMeetingUrl { get; set; }
}