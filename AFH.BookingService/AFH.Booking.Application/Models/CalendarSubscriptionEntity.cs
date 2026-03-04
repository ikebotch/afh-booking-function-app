namespace AFH.Booking.Application.Calendar.Models;

public sealed class CalendarSubscriptionEntity
{
    public string AdviserId { get; set; } = default!;
    public string SubscriptionId { get; set; } = default!;
    public DateTime ExpiresUtc { get; set; }
    public string Resource { get; set; } = default!;
    public string? ClientState { get; set; }
}