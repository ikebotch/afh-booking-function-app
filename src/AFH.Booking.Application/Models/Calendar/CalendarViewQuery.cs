namespace AFH.Booking.Application.Models.Calendar;

public sealed class CalendarViewQuery
{
    public IReadOnlyList<AdviserProjectionItem> AdviserList { get; set; } = [];
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string Timezone { get; set; } = "Europe/London";
}
