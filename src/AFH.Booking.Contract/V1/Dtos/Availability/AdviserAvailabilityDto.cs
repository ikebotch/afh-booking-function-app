namespace AFH.Booking.Contracts.V1.Dtos.Availability;

public sealed class AdviserAvailabilityDto
{
    public string AdviserId { get; init; } = default!;
    public string AdviserName { get; init; } = default!;
    public IReadOnlyList<SlotDto> Slots { get; init; } = [];
}
