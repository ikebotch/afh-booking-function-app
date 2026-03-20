using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Transactions;
using System.Globalization;

namespace AFH.Booking.Application.EmailTemplates;

public static class ConfirmedBookingTemplate
{
    public static string BuildConfirmedBodyTemplate(
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

        return
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
}