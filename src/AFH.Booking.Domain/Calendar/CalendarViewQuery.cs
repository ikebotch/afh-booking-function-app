namespace AFH.Booking.Domain.Calendar;

public sealed class CalendarViewQuery
{
    public IReadOnlyList<AdviserDirectoryItem> AdviserList { get; set; } = [];
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string Timezone { get; set; } = "Europe/London";
}
