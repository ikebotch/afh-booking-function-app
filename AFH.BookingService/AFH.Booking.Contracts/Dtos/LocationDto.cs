namespace AFH.Booking.Contracts.Dtos;

public sealed record LocationDto
{
    public string? DisplayName { get; init; }
    public string? AddressLine1 { get; init; }
    public string? City { get; init; }
    public string? Postcode { get; init; }

};
