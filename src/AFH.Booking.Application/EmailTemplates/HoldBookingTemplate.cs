using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Transactions;
using System.Globalization;

namespace AFH.Booking.Application.EmailTemplates;

public static class HoldBookingTemplate
{
    public static string BuildHoldBodyTemplate(
        BookingSlot slot,
        BookingTransaction tx,
        BookingHold hold,
        HoldWindows windows)
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
                ? $"Travel buffer: {windows.TravelBufferMinutesEachSide} mins"
                : "Travel buffer: none";

        var companyLine = windows.CompanyBufferMinutes > 0
            ? $"Company buffer: {windows.CompanyBufferMinutes} mins (pre/post meeting policy)"
            : "Company buffer: none";

        return
$@"AFH Booking (HOLD)

TransactionRef: {tx.TransactionRef}
HoldId: {hold.Id}
AdviserId: {slot.AdviserId}
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

Notes:
- Temporary hold while booking is being confirmed.
- This hold should block overlapping bookings.";
    }

    private static string FormatUtc(DateTime utc)
        => utc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "Z";

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

}

public sealed record HoldWindows(
    DateTime HoldStartUtc,
    DateTime HoldEndUtc,
    int TravelBufferMinutesEachSide,
    int CompanyBufferMinutes,
    bool TravelApplied);
