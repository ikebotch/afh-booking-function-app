namespace AFH.Booking.Contracts.V1.Dtos.Availability;

public sealed class AvailabilityWarningDto
{
    public string Code { get; init; } = default!;
    public string Message { get; init; } = default!;
}