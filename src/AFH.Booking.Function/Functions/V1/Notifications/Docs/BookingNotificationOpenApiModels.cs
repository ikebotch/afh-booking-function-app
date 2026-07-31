using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Booking.Function.Functions.V1.Notifications.Docs;

internal sealed class BookingNotificationSubmitRequestExample
{
    public NotificationType Type { get; init; } = new("Booking", "BookingConfirmed");
    public string CorrelationId { get; init; } = "booking-123-confirmed";
    public NotificationActor Actor { get; init; } = new("System", "Booking", null, null, null);
    public IReadOnlyList<BookingNotificationRecipientExample> Recipients { get; init; } = [];
    public BookingNotificationPayloadExample Data { get; init; } = new();
    public string? SourceApplication { get; init; }
    public string? NotificationType { get; init; }
    public string? SourceReferenceType { get; init; }
    public string? SourceReferenceId { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? TemplateKey { get; init; }
    public string? TemplateVersion { get; init; }
    public IReadOnlyList<NotificationChannel>? Channels { get; init; }
}

internal sealed class BookingNotificationRecipientExample
{
    public string RecipientType { get; init; } = "Client";
    public string? DisplayName { get; init; } = "Jane Client";
    public string? Email { get; init; } = "jane.client@example.test";
    public string? MobileNumber { get; init; }
    public string? PushTarget { get; init; }
    public IReadOnlyList<NotificationChannel>? PreferredChannels { get; init; } = [NotificationChannel.Email];
    public IReadOnlyList<NotificationChannel>? Channels { get; init; }
}

internal sealed class BookingNotificationPayloadExample
{
    public string EventId { get; init; } = "booking-123-confirmed";
    public string BookingId { get; init; } = "booking-123";
    public string HoldId { get; init; } = "booking-123";
    public string SlotId { get; init; } = "slot-123";
    public string AdviserId { get; init; } = "adv-001";
    public string AdviserName { get; init; } = "John Doe";
    public string TransactionRef { get; init; } = "TRX-123";
    public string StartUtc { get; init; } = "2026-07-01T09:00:00Z";
    public string EndUtc { get; init; } = "2026-07-01T10:00:00Z";
    public string ClientName { get; init; } = "Jane Client";
    public string ClientEmail { get; init; } = "jane.client@example.test";
    public string ClientPhone { get; init; } = "+447700900123";
    public string MeetingType { get; init; } = "Review";
    public string MeetingTopic { get; init; } = "Review";
    public string MeetingDate { get; init; } = "Wed 01 Jul 2026";
    public string MeetingDateDay { get; init; } = "Wed 01 Jul 2026";
    public string MeetingDateTime { get; init; } = "09:00-10:00 (Europe/London)";
    public string Date { get; init; } = "Wed 01 Jul 2026";
    public string Time { get; init; } = "09:00-10:00 (Europe/London)";
    public string MeetingMethod { get; init; } = "Online";
    public string MeetingDuration { get; init; } = "60 minutes";
    public string MeetingStatus { get; init; } = "Confirmed";
    public string MeetingAddress { get; init; } = "42 King Street, Manchester, M2 4LQ";
    public string MeetingAddressLine1 { get; init; } = "42 King Street";
    public string MeetingAddressLine2 { get; init; } = "";
    public string MeetingTown { get; init; } = "Manchester";
    public string MeetingCounty { get; init; } = "";
    public string MeetingPostcode { get; init; } = "M2 4LQ";
    public string When { get; init; } = "Wed 01 Jul 2026 09:00 (Europe/London) to Wed 01 Jul 2026 10:00 (Europe/London)";
    public string WhenLine { get; init; } = "Wed 01 Jul 2026 09:00 (Europe/London) to Wed 01 Jul 2026 10:00 (Europe/London)";
    public string WhereLine { get; init; } = "Join link: https://meet.example.test/booking-123";
    public string LocationLine { get; init; } = "Online";
    public string TravelLine { get; init; } = "Travel: N/A (remote meeting)";
    public string JoinUrl { get; init; } = "https://meet.example.test/booking-123";
    public string JoinMeetingLink { get; init; } = "https://meet.example.test/booking-123";
    public string ManageBookingLink { get; init; } = "https://portal.example.test/bookings/booking-123";
    public string ManageBookingLinks { get; init; } = "Manage your booking:\n- View booking: https://portal.example.test/bookings/booking-123";
    public string ViewBookingUrl { get; init; } = "https://portal.example.test/bookings/booking-123";
    public string CancelBookingUrl { get; init; } = "https://portal.example.test/bookings/booking-123/cancel";
    public string RescheduleBookingUrl { get; init; } = "https://portal.example.test/bookings/booking-123/reschedule";
    public string ContactNumber { get; init; } = "0800 000 0000";
    public string ContactUsNumber { get; init; } = "0800 000 0000";
    public string RecipientType { get; init; } = "Client";
}
