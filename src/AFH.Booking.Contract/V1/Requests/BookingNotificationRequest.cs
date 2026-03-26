namespace AFH.Booking.Contracts.V1.Requests;

public sealed class BookingNotificationRequest
{
    public string EventType { get; init; } = "BookingChanged";
    public bool SendSms { get; init; } = true;
    public bool SendEmail { get; init; } = true;
    public string? MessageOverride { get; init; }
}
