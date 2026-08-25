using System.Globalization;
using System.Text;
using AFH.Booking.Application.Models.Lifecycle.Constants;
using AFH.Booking.Application.Models.Notifications;

namespace AFH.Booking.Application.Services.Lifecycle;

internal static class BookingCalendarInviteAttachmentFactory
{
    private const string ProductId = "-//AFH//Booking Notifications//EN";

    public static IReadOnlyList<BookingNotificationAttachment>? Create(
        string lifecycleEventType,
        IReadOnlyDictionary<string, string> data,
        string recipientType,
        DateTime utcNow)
    {
        if (!ShouldAttachToRecipient(recipientType) || !TryResolveMethod(lifecycleEventType, out var method, out var status, out var sequence))
            return null;

        if (!TryGetUtc(data, "startUtc", out var startUtc) || !TryGetUtc(data, "endUtc", out var endUtc))
            return null;

        var bookingId = GetValue(data, "bookingId");
        if (string.IsNullOrWhiteSpace(bookingId))
            return null;

        var content = BuildContent(data, bookingId, method, status, sequence, startUtc, endUtc, utcNow);
        var fileName = $"booking-{SanitizeFileName(bookingId)}.ics";

        return
        [
            new BookingNotificationAttachment(
                fileName,
                $"text/calendar; charset=utf-8; method={method}",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
                Channels: [BookingNotificationChannel.Email])
        ];
    }

    private static string BuildContent(
        IReadOnlyDictionary<string, string> data,
        string bookingId,
        string method,
        string status,
        int sequence,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        DateTime utcNow)
    {
        var summary = GetValue(data, "meetingTopic")
            ?? GetValue(data, "meetingType")
            ?? "AFH appointment";
        var adviserName = GetValue(data, "adviserName");
        var transactionRef = GetValue(data, "transactionRef");
        var joinUrl = GetValue(data, "joinMeetingLink") ?? GetValue(data, "joinUrl");
        var location = GetValue(data, "meetingAddress")
            ?? GetValue(data, "locationLine")
            ?? (string.IsNullOrWhiteSpace(joinUrl) ? null : joinUrl)
            ?? "AFH";

        var descriptionParts = new[]
            {
                string.IsNullOrWhiteSpace(adviserName) ? null : $"Adviser: {adviserName}",
                string.IsNullOrWhiteSpace(transactionRef) ? null : $"Reference: {transactionRef}",
                string.IsNullOrWhiteSpace(joinUrl) ? null : $"Join: {joinUrl}"
            }
            .Where(x => !string.IsNullOrWhiteSpace(x));

        var description = string.Join("\\n", descriptionParts);

        var builder = new StringBuilder();
        Append(builder, "BEGIN:VCALENDAR");
        Append(builder, "VERSION:2.0");
        Append(builder, $"PRODID:{ProductId}");
        Append(builder, "CALSCALE:GREGORIAN");
        Append(builder, $"METHOD:{method}");
        Append(builder, "BEGIN:VEVENT");
        Append(builder, $"UID:booking-{Escape(bookingId)}@afh-booking");
        Append(builder, $"DTSTAMP:{FormatUtc(utcNow)}");
        Append(builder, $"DTSTART:{FormatUtc(startUtc.UtcDateTime)}");
        Append(builder, $"DTEND:{FormatUtc(endUtc.UtcDateTime)}");
        Append(builder, $"SUMMARY:{Escape($"AFH meeting: {summary}")}");
        Append(builder, $"DESCRIPTION:{Escape(description)}");
        Append(builder, $"LOCATION:{Escape(location)}");
        Append(builder, $"STATUS:{status}");
        Append(builder, $"SEQUENCE:{sequence}");
        Append(builder, "END:VEVENT");
        Append(builder, "END:VCALENDAR");
        return builder.ToString();
    }

    private static bool ShouldAttachToRecipient(string recipientType)
        => recipientType.Equals(BookingNotificationRecipientTypes.Client, StringComparison.OrdinalIgnoreCase)
           || recipientType.Equals(BookingNotificationRecipientTypes.Adviser, StringComparison.OrdinalIgnoreCase);

    private static bool TryResolveMethod(string lifecycleEventType, out string method, out string status, out int sequence)
    {
        switch (lifecycleEventType)
        {
            case LifecycleEventTypes.Booked:
            case BookingNotificationTypes.BookingConfirmedName:
                method = "REQUEST";
                status = "CONFIRMED";
                sequence = 0;
                return true;
            case LifecycleEventTypes.Rearranged:
            case BookingNotificationTypes.BookingRescheduledName:
                method = "REQUEST";
                status = "CONFIRMED";
                sequence = 1;
                return true;
            case LifecycleEventTypes.Cancelled:
            case BookingNotificationTypes.BookingCancelledName:
                method = "CANCEL";
                status = "CANCELLED";
                sequence = 2;
                return true;
            default:
                method = string.Empty;
                status = string.Empty;
                sequence = 0;
                return false;
        }
    }

    private static bool TryGetUtc(IReadOnlyDictionary<string, string> data, string key, out DateTimeOffset value)
    {
        value = default;
        return data.TryGetValue(key, out var raw)
               && DateTimeOffset.TryParse(
                   raw,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                   out value);
    }

    private static string? GetValue(IReadOnlyDictionary<string, string> data, string key)
        => data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static string FormatUtc(DateTime value)
        => value.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Escape(string? value)
        => (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\n", StringComparison.Ordinal);

    private static string SanitizeFileName(string value)
    {
        var sanitized = new string(value
            .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "booking" : sanitized;
    }

    private static void Append(StringBuilder builder, string line)
        => builder.Append(line).Append("\r\n");
}
