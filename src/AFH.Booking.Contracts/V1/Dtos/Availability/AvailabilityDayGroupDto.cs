using AFH.Booking.Contracts.V1.Dtos;
using AFH.Booking.Contracts.V1.Responses;

namespace AFH.Booking.Contracts.V1.Dtos.Availability;

public sealed class AvailabilityDayGroupDto
{
    public DateOnly DateUtc { get; init; }
    public List<AvailabilityAdviserDto> Advisers { get; init; } = new();
    public int TotalSlots { get; init; }
    public int TotalAdvisers { get; init; }
    public List<AvailabilityWarningDto> Warnings { get; init; } = new();
}