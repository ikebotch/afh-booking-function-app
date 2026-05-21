namespace AFH.Booking.Application.Models.Availability;

public sealed class AdviserSlotsDto
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public bool GoldStar { get; init; }
    public IReadOnlyList<SlotDto> Slots { get; init; } = [];
}
