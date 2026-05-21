namespace AFH.Booking.Application.Models.Availability;

public sealed class AvailabilityAdviserDto
{
    public string AdviserId { get; set; } = default!;
    public string AdviserName { get; set; } = default!;
    public string? Region { get; set; }
    public List<SlotDto> Slots { get; set; } = new();
}
