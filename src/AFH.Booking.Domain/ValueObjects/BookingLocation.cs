namespace AFH.Booking.Domain.ValueObjects;

public sealed class BookingLocation
{
    public string? DisplayName { get; init; }
    public string? AddressLine1 { get; init; }
    public string? City { get; init; }
    public string? Postcode { get; init; }
    public string? Address { get; init; }
}