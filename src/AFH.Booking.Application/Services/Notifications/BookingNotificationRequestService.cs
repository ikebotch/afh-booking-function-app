using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Notifications;
using AFH.Booking.Application.Models.Notifications;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Booking.Application.Services.Notifications;

public sealed class BookingNotificationRequestService : IBookingNotificationRequestService
{
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _transactions;
    private readonly IClientDirectory _clients;
    private readonly INotificationPublisher _publisher;

    public BookingNotificationRequestService(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        IClientDirectory clients,
        INotificationPublisher publisher)
    {
        _holds = holds;
        _slots = slots;
        _transactions = transactions;
        _clients = clients;
        _publisher = publisher;
    }

    public async Task<Result<NotificationDispatchResponse>> SendAsync(
        string bookingId,
        string eventType,
        string? messageOverride,
        bool sendSms,
        bool sendEmail,
        string? correlationId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingId))
            return Result<NotificationDispatchResponse>.Fail(HttpStatusCode.BadRequest, "bookingId is required.", Errors.Validation);

        if (sendSms)
            return Result<NotificationDispatchResponse>.Fail(HttpStatusCode.BadRequest, "SMS is not supported by queued booking notifications yet.", Errors.Validation);

        if (!sendEmail)
            return Result<NotificationDispatchResponse>.Fail(HttpStatusCode.BadRequest, "At least one supported notification channel must be requested.", Errors.Validation);

        if (!string.IsNullOrWhiteSpace(messageOverride))
            return Result<NotificationDispatchResponse>.Fail(HttpStatusCode.BadRequest, "MessageOverride is not supported by queued booking notification templates yet.", Errors.Validation);

        var notificationType = MapEventType(eventType);
        if (notificationType is null)
        {
            return Result<NotificationDispatchResponse>.Fail(
                HttpStatusCode.BadRequest,
                "Unsupported EventType. Supported values are Booked, BookingConfirmed, Rearranged, BookingRescheduled, Cancelled, BookingCancelled, HoldCreated, and BookingHoldCreated.",
                Errors.Validation);
        }

        var hold = await _holds.GetAsync(bookingId.Trim(), ct);
        if (hold is null)
            return Result<NotificationDispatchResponse>.NotFound($"Hold '{bookingId}' was not found.");

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
            return Result<NotificationDispatchResponse>.Fail(HttpStatusCode.Conflict, $"Slot '{hold.SlotId}' was not found.", Errors.Conflict);

        var transaction = await _transactions.GetAsync(slot.TransactionId, ct);
        if (transaction is null)
            return Result<NotificationDispatchResponse>.Fail(HttpStatusCode.Conflict, $"Transaction '{slot.TransactionId}' was not found.", Errors.Conflict);

        var client = await _clients.GetAsync(transaction.TransactionRef, ct);
        var publishCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? $"manual-{hold.Id}-{Guid.NewGuid():N}"
            : correlationId.Trim();

        await _publisher.PublishAsync(
            new NotificationRequested(
                notificationType,
                publishCorrelationId,
                new NotificationActor("Internal", "Booking", null, "Manual notification", null),
                BuildRecipients(client),
                BuildData(notificationType, hold, slot, transaction)),
            ct);

        return Result<NotificationDispatchResponse>.Ok(new NotificationDispatchResponse
        {
            DispatchId = publishCorrelationId,
            BookingId = hold.Id,
            EventType = notificationType.Name,
            SmsRequested = false,
            EmailRequested = true,
            SmsStatus = "Skipped",
            EmailStatus = "Queued",
            ProviderMessageId = null,
            CreatedUtc = DateTime.UtcNow
        });
    }

    private static NotificationType? MapEventType(string? eventType)
    {
        var value = eventType?.Trim();
        return value switch
        {
            "Booked" or "BookingConfirmed" => BookingNotificationTypes.BookingConfirmed,
            "Rearranged" or "BookingRescheduled" => BookingNotificationTypes.BookingRescheduled,
            "Cancelled" or "BookingCancelled" => BookingNotificationTypes.BookingCancelled,
            "HoldCreated" or "BookingHoldCreated" => BookingNotificationTypes.BookingHoldCreated,
            _ => null
        };
    }

    private static IReadOnlyList<NotificationRecipient> BuildRecipients(Domain.Client.ClientDirectoryItem? client)
    {
        if (client is null)
            return Array.Empty<NotificationRecipient>();

        var displayName = $"{client.FirstName} {client.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = null;

        return
        [
            new NotificationRecipient(
                BookingNotificationRecipientTypes.Client,
                displayName,
                client.Email,
                client.Phone,
                null,
                [NotificationChannel.Email])
        ];
    }

    private static IReadOnlyDictionary<string, string> BuildData(
        NotificationType type,
        BookingHold hold,
        BookingSlot slot,
        BookingTransaction transaction)
    {
        var meetingType = transaction.IsRemote ? "Remote meeting" : "In-person meeting";
        var when = $"{FormatLocal(slot.StartUtc, transaction.Timezone)} to {FormatLocal(slot.EndUtc, transaction.Timezone)}";
        var locationLine = meetingType;
        var note = type.Name switch
        {
            "BookingCancelled" => "Your meeting has been cancelled.",
            "BookingRescheduled" => "Your meeting has been rescheduled.",
            _ => string.Empty
        };

        return new Dictionary<string, string>
        {
            ["transactionRef"] = transaction.TransactionRef,
            ["bookingId"] = hold.Id,
            ["holdId"] = hold.Id,
            ["slotId"] = slot.Id,
            ["adviserId"] = slot.AdviserId,
            ["adviserName"] = slot.AdviserName,
            ["startUtc"] = slot.StartUtc.ToString("O"),
            ["endUtc"] = slot.EndUtc.ToString("O"),
            ["meetingType"] = meetingType,
            ["when"] = when,
            ["whenLine"] = when,
            ["whereLine"] = transaction.IsRemote ? "Remote meeting" : "Location: To be confirmed",
            ["locationLine"] = locationLine,
            ["travelLine"] = transaction.IsRemote ? "Travel: N/A (remote meeting)" : string.Empty,
            ["companyLine"] = string.Empty,
            ["holdExpires"] = FormatLocal(hold.ExpiresUtc, transaction.Timezone),
            ["manageBookingLinks"] = string.Empty,
            ["greetingName"] = "there",
            ["note"] = note
        };
    }

    private static string FormatLocal(DateTime utc, string? timezoneId)
    {
        var tz = string.IsNullOrWhiteSpace(timezoneId) ? "UTC" : timezoneId.Trim();
        try
        {
            if (tz.Equals("UTC", StringComparison.OrdinalIgnoreCase))
                return utc.ToUniversalTime().ToString("ddd dd MMM yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) + " UTC";

            var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(tz);
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tzInfo);
            return local.ToString("ddd dd MMM yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) + $" ({tz})";
        }
        catch
        {
            return utc.ToUniversalTime().ToString("ddd dd MMM yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) + " UTC";
        }
    }
}
