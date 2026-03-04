namespace AFH.Booking.Contracts.Responses;

public sealed class CalendarSubscriptionDto
{
    public string? SubscriptionId { get; set; }
    public string? AdviserId { get; set; }
    public string? Resource { get; set; }
    public DateTime ExpirationUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
