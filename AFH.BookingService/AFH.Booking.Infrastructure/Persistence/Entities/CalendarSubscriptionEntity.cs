namespace AFH.Booking.Infrastructure.Persistence.Entities;

public sealed class CalendarSubscriptionEntity
{
    public string SubscriptionId { get; set; } = default!;
    public string AdviserId { get; set; } = default!;
    public string Resource { get; set; } = default!;
    public DateTime ExpirationUtc { get; set; }

    public string ClientState { get; set; } = default!;

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}