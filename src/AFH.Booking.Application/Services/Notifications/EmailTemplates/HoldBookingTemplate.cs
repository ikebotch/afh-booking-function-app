using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Domain.Calendar;
using System.Globalization;

namespace AFH.Booking.Application.EmailTemplates;

public static class HoldBookingTemplate
{
    public static NotificationTemplateContent BuildHoldTemplate(
        BookingSlot slot,
        BookingTransaction tx,
        BookingHold hold,
        HoldWindows windows,
        BookingSelfServiceLinks? selfServiceLinks = null)
    {
        var tzId = string.IsNullOrWhiteSpace(tx.Timezone) ? "UTC" : tx.Timezone.Trim();

        var slotStartLocal = FormatLocal(slot.StartUtc, tzId);
        var slotEndLocal = FormatLocal(slot.EndUtc, tzId);

        var holdStartLocal = FormatLocal(windows.HoldStartUtc, tzId);
        var holdEndLocal = FormatLocal(windows.HoldEndUtc, tzId);

        var slotStartUtc = FormatUtc(slot.StartUtc);
        var slotEndUtc = FormatUtc(slot.EndUtc);

        var holdStartUtc = FormatUtc(windows.HoldStartUtc);
        var holdEndUtc = FormatUtc(windows.HoldEndUtc);

        var travelLine = tx.IsRemote
            ? "Travel: N/A (remote meeting)"
            : windows.TravelApplied
                ? $"Travel time: {windows.TravelMinutes} mins before"
                : "Travel buffer: none";

        var companyLine = windows.CompanyBufferMinutes > 0
            ? $"Company buffer: {windows.CompanyBufferMinutes} mins (pre/post meeting policy)"
            : "Company buffer: none";
        var textLinks = BuildTextLinks(selfServiceLinks);
        var htmlLinks = BuildHtmlLinks(selfServiceLinks);

        var subject = "AFH Booking: Hold Created";
        var textBody =
$@"Hello,

We have placed a temporary hold on your requested booking while it is being confirmed.

Transaction reference: {tx.TransactionRef}
Hold ID: {hold.Id}
Adviser: {slot.AdviserName}
Meeting type: {(string.IsNullOrWhiteSpace(tx.MeetingType) ? "N/A" : tx.MeetingType)}
When: {slotStartLocal} -> {slotEndLocal}
Hold expires: {FormatUtc(hold.ExpiresUtc)}

{travelLine}
{companyLine}
{textLinks}

This is an automated AFH booking notification.";

        var calendarDescription =
$@"AFH Booking Hold

TransactionRef: {tx.TransactionRef}
HoldId: {hold.Id}
Adviser: {slot.AdviserName}
Meeting type: {(string.IsNullOrWhiteSpace(tx.MeetingType) ? "N/A" : tx.MeetingType)}
Remote: {(tx.IsRemote ? "Yes" : "No")}
Timezone: {tzId}

Actual meeting time:
- Local: {slotStartLocal} -> {slotEndLocal}
- UTC:   {slotStartUtc} -> {slotEndUtc}

{travelLine}
{companyLine}

Calendar block (hold window):
- Local: {holdStartLocal} -> {holdEndLocal}
- UTC:   {holdStartUtc} -> {holdEndUtc}

Hold expires (UTC): {FormatUtc(hold.ExpiresUtc)}
{textLinks}

Notes:
- Temporary hold while booking is being confirmed.
- This hold should block overlapping bookings.";

        var htmlBody =
$@"<!doctype html>
<html lang=""en"">
  <head><meta charset=""utf-8"" /><meta name=""viewport"" content=""width=device-width, initial-scale=1"" /><title>{Escape(subject)}</title></head>
  <body style=""font-family:'Segoe UI',Arial,sans-serif;color:#1f2937;"">
    <h1 style=""font-size:22px;"">Temporary booking hold created</h1>
    <p>We have placed a temporary hold on your requested booking while it is being confirmed.</p>
    <ul>
      <li><strong>Transaction reference:</strong> {Escape(tx.TransactionRef)}</li>
      <li><strong>Hold ID:</strong> {Escape(hold.Id)}</li>
      <li><strong>Adviser:</strong> {Escape(slot.AdviserName)}</li>
      <li><strong>Meeting type:</strong> {Escape(string.IsNullOrWhiteSpace(tx.MeetingType) ? "N/A" : tx.MeetingType)}</li>
      <li><strong>When:</strong> {Escape(slotStartLocal)} -> {Escape(slotEndLocal)}</li>
      <li><strong>Hold expires:</strong> {Escape(FormatUtc(hold.ExpiresUtc))}</li>
    </ul>
    <p>{Escape(travelLine)}</p>
    <p>{Escape(companyLine)}</p>
    {htmlLinks}
    <p>This is an automated AFH booking notification.</p>
  </body>
</html>";

        return new NotificationTemplateContent(subject, htmlBody, textBody, calendarDescription);
    }

    private static string FormatUtc(DateTime utc)
        => utc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "Z";

    private static string BuildTextLinks(BookingSelfServiceLinks? links)
    {
        if (links is null)
            return string.Empty;

        return
$@"
Manage your booking:
- View booking: {links.ViewBookingUrl}
- Cancel booking: {links.CancelBookingUrl}
- Reschedule booking: {links.RescheduleBookingUrl}";
    }

    private static string BuildHtmlLinks(BookingSelfServiceLinks? links)
    {
        if (links is null)
            return string.Empty;

        return
$@"<div style=""margin:18px 0;padding:14px;border:1px solid #cbd5e1;border-radius:8px;"">
      <p style=""margin:0 0 10px;font-weight:600;"">Manage your booking</p>
      <p style=""margin:0 0 8px;""><a href=""{Escape(links.ViewBookingUrl)}"">View booking</a></p>
      <p style=""margin:0 0 8px;""><a href=""{Escape(links.CancelBookingUrl)}"">Cancel booking</a></p>
      <p style=""margin:0;""><a href=""{Escape(links.RescheduleBookingUrl)}"">Reschedule booking</a></p>
    </div>";
    }

    private static string FormatLocal(DateTime utc, string timezoneId)
    {
        // Best-effort: if timezoneId isn’t recognised, fall back to UTC.
        try
        {
            if (timezoneId.Equals("UTC", StringComparison.OrdinalIgnoreCase))
                return FormatUtc(utc);

            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);

            // Example: 2026-02-19 17:30 (Europe/London)
            return local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + $" ({timezoneId})";
        }
        catch
        {
            return FormatUtc(utc);
        }
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

public sealed record HoldWindows(
    DateTime HoldStartUtc,
    DateTime HoldEndUtc,
    int TravelBufferMinutesEachSide,
    int CompanyBufferMinutes,
    bool TravelApplied)
{
    public int TravelMinutes => TravelBufferMinutesEachSide;
    public bool HasBuffer => TravelApplied;
}
