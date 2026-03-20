namespace AFH.Booking.Domain.Location;

public sealed class LocationAddress
{
    public string Line1 { get; set; } = default!;
    public string Town { get; set; } = default!;
    public string Postcode { get; set; } = default!;
    public string Country { get; set; } = "UK";
}


