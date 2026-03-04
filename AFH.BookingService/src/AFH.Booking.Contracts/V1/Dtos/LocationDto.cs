namespace AFH.Booking.Contracts.V1.Dtos;

public sealed class LocationDto
{
    public string? DisplayName { get; init; }
    public string? AddressLine1 { get; init; }
    public string? City { get; init; }
    public string? Postcode { get; init; }
}
