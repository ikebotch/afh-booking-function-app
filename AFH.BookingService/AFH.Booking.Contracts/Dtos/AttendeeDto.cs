namespace AFH.Booking.Contracts.Dtos;

public sealed record AttendeeDto(
    string Email,
    string? DisplayName = null,
    bool Required = true
);
