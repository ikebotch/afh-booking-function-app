namespace AFH.Booking.Contracts.V1.Dtos;

public sealed class AttendeeDto
{
    public string Email { get; init; } = default!;
    public string? Name { get; init; }
    public string Type { get; init; } = "Required";
}
