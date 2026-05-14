namespace AFH.Booking.Domain.Bookings;

public sealed class LocationModel
{
    public string Id { get; set; } = default!;          // "bristol-office" or GUID

    public string DisplayName { get; set; } = default!;
    public string? AddressLine1 { get; set; }
    public string? City { get; set; }
    public string? Postcode { get; set; }
    public string? Address { get; set; }                // preformatted if you want

    public bool IsActive { get; set; } = true;

    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}