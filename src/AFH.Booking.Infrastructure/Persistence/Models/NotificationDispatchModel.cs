namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class NotificationDispatchModel
{
    public string Id { get; set; } = default!;
    public string BookingId { get; set; } = default!;
    public string EventType { get; set; } = default!;
    public bool SmsRequested { get; set; }
    public bool EmailRequested { get; set; }
    public string SmsStatus { get; set; } = default!;
    public string EmailStatus { get; set; } = default!;
    public string? RecipientPhone { get; set; }
    public string? RecipientEmail { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? MessageBody { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}
