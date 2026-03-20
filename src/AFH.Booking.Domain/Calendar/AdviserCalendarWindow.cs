namespace AFH.Booking.Domain.Calendar;
public sealed class AdviserCalendarWindow
{
    public string AdviserId { get; init; } = default!;
    public IReadOnlyList<BusyBlock> BusyBlocks { get; init; } = [];
}
