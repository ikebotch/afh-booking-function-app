using AFH.Booking.Domain.Calendar;
using System.Globalization;

namespace AFH.Booking.Application.EmailTemplates;

public static class ConfirmedBookingTemplate
{
    public static NotificationTemplateContent BuildConfirmedTemplate(
        BookingSlot slot,
        BookingTransaction tx,
        BookingHold booking,        
        HoldWindows windows,
        string? joinUrl = null,       
        CalendarLocation? location = null) 
    {
        var tzId = string.IsNullOrWhiteSpace(tx.Timezone) ? "UTC" : tx.Timezone.Trim();

        // Actual meeting time (slot) + calendar block (windows)
        var slotLocal = $"{FormatLocal(slot.StartUtc, tzId)} → {FormatLocal(slot.EndUtc, tzId)}";
        var slotUtc = $"{FormatUtc(slot.StartUtc)} → {FormatUtc(slot.EndUtc)}";

        var blockLocal = $"{FormatLocal(windows.HoldStartUtc, tzId)} → {FormatLocal(windows.HoldEndUtc, tzId)}";
        var blockUtc = $"{FormatUtc(windows.HoldStartUtc)} → {FormatUtc(windows.HoldEndUtc)}";

        var travelLine = tx.IsRemote
            ? "Travel: N/A (remote meeting)"
            : windows.TravelApplied
                ? $"Travel buffer: {windows.TravelBufferMinutesEachSide} mins before + {windows.TravelBufferMinutesEachSide} mins after"
                : "Travel buffer: none";

        var whereLine = tx.IsRemote
            ? $"Join link: {(string.IsNullOrWhiteSpace(joinUrl) ? "TBC" : joinUrl)}"
            : $"Location: {FormatLocation(location)}";

        var subject = "AFH Booking: Booking Confirmed";
        var textBody =
$@"Hello,

Your booking is now confirmed.

Transaction reference: {tx.TransactionRef}
Booking ID: {booking.Id}
Adviser: {slot.AdviserName}
Meeting type: {(string.IsNullOrWhiteSpace(tx.MeetingType) ? "N/A" : tx.MeetingType)}
When: {slotLocal}
{whereLine}

{travelLine}

This is an automated AFH booking notification.";

        var calendarDescription =
$@"AFH Booking (CONFIRMED)

TransactionRef: {tx.TransactionRef}
BookingId: {booking.Id}
AdviserId: {slot.AdviserId}
Meeting type: {(string.IsNullOrWhiteSpace(tx.MeetingType) ? "N/A" : tx.MeetingType)}
Remote: {(tx.IsRemote ? "Yes" : "No")}
Timezone: {tzId}

When:
- Local: {slotLocal}
- UTC:   {slotUtc}

Where:
- {whereLine}

{travelLine}

Calendar block:
- Local: {blockLocal}
- UTC:   {blockUtc}

Notes:
- This booking is confirmed.
- Please allow time for travel and preparation either side of the meeting.";

        var htmlBody =
$@"<!doctype html>
<html lang=""en"">
  <head><meta charset=""utf-8"" /><meta name=""viewport"" content=""width=device-width, initial-scale=1"" /><title>{Escape(subject)}</title></head>
  <body style=""font-family:'Segoe UI',Arial,sans-serif;color:#1f2937;"">
    <h1 style=""font-size:22px;"">Your booking is confirmed</h1>
    <ul>
      <li><strong>Transaction reference:</strong> {Escape(tx.TransactionRef)}</li>
      <li><strong>Booking ID:</strong> {Escape(booking.Id)}</li>
      <li><strong>Adviser:</strong> {Escape(slot.AdviserName)}</li>
      <li><strong>Meeting type:</strong> {Escape(string.IsNullOrWhiteSpace(tx.MeetingType) ? "N/A" : tx.MeetingType)}</li>
      <li><strong>When:</strong> {Escape(slotLocal)}</li>
      <li><strong>Where:</strong> {Escape(whereLine)}</li>
    </ul>
    <p>{Escape(travelLine)}</p>
    <p>This is an automated AFH booking notification.</p>
  </body>
</html>";

        return new NotificationTemplateContent(subject, htmlBody, textBody, calendarDescription);
    }

    private static string FormatUtc(DateTime utc)
        => utc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "Z";

    private static string FormatLocal(DateTime utc, string timezoneId)
    {
        try
        {
            if (timezoneId.Equals("UTC", StringComparison.OrdinalIgnoreCase))
                return FormatUtc(utc);

            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);

            return local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + $" ({timezoneId})";
        }
        catch
        {
            return FormatUtc(utc);
        }
    }

    private static string FormatLocation(CalendarLocation? location)
    {
        if (location is null)
            return "TBC";

        var parts = new[]
        {
            location.DisplayName,
            location.AddressLine1,
            location.City,
            location.Postcode
        }.Where(x => !string.IsNullOrWhiteSpace(x));

        return string.Join(", ", parts);
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
    }
}
