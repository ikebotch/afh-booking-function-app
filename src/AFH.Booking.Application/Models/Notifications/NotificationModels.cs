namespace AFH.Booking.Application.Models.Notifications;

public sealed class NotificationDispatchResponse
{
    public string DispatchId { get; init; } = default!;
    public string BookingId { get; init; } = default!;
    public string EventType { get; init; } = default!;
    public bool SmsRequested { get; init; }
    public bool EmailRequested { get; init; }
    public string SmsStatus { get; init; } = default!;
    public string EmailStatus { get; init; } = default!;
    public string? ProviderMessageId { get; init; }
    public DateTime CreatedUtc { get; init; }
}
