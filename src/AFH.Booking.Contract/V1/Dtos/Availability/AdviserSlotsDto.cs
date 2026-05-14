namespace AFH.Booking.Contracts.V1.Dtos.Availability;

public sealed class AdviserSlotsDto
{
    public string Id { get; init; } = default!;   // internal ID, NOT email
    public string Name { get; init; } = default!;
    public bool GoldStar { get; init; }
 
    public IReadOnlyList<SlotDto> Slots { get; init; } = [];

}
