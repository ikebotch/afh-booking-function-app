namespace AFH.Booking.Infrastructure.Persistence.Models;


public sealed class CalendarSubscriptionModel
{
    public string Id { get; set; }
    public string SubscriptionId { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public string Resource { get; set; } = default!;
    public string NotificationUrl { get; set; } = default!;
    public string ClientState { get; set; } = default!;
    public DateTime ExpirationUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }


    // Concurrency
    public byte[] RowVersion { get; set; } = default!;
}
