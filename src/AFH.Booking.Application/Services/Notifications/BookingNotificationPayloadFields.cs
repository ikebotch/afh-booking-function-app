using AFH.Booking.Domain.Bookings;
using System.Globalization;

namespace AFH.Booking.Application.Services.Notifications;

internal static class BookingNotificationPayloadFields
{
    public static void AddStandardBookingFields(
        IDictionary<string, string> data,
        BookingTransaction transaction,
        BookingSlot slot,
        string meetingStatus,
        string? joinMeetingLink = null,
        string? manageBookingLink = null,
        string? contactNumber = null)
    {
        var meetingTopic = FirstNonEmpty(transaction.MeetingType, "N/A");
        var meetingMethod = transaction.IsRemote ? "Online" : "Face to face";
        var dayPart = FormatLocalDate(slot.StartUtc, transaction.Timezone);
        var timePart = FormatLocalTimeRange(slot.StartUtc, slot.EndUtc, transaction.Timezone);

        data["meetingType"] = meetingTopic;
        data["meetingTopic"] = meetingTopic;
        data["meetingMethod"] = meetingMethod;
        data["meetingDate"] = dayPart;
        data["meetingDateDay"] = dayPart;
        data["meetingDateTime"] = timePart;
        data["date"] = dayPart;
        data["time"] = timePart;
        data["meetingDuration"] = FormatDuration(slot.EndUtc - slot.StartUtc);
        data["meetingStatus"] = meetingStatus;
        data["joinMeetingLink"] = joinMeetingLink?.Trim() ?? string.Empty;
        data["joinUrl"] = data.TryGetValue("joinUrl", out var existingJoinUrl) && !string.IsNullOrWhiteSpace(existingJoinUrl)
            ? existingJoinUrl
            : joinMeetingLink?.Trim() ?? string.Empty;
        data["manageBookingLink"] = manageBookingLink?.Trim() ?? string.Empty;
        data["contactNumber"] = contactNumber?.Trim() ?? string.Empty;
        data["contactUsNumber"] = contactNumber?.Trim() ?? string.Empty;
    }

    private static string FormatLocalDate(DateTime utc, string? timezoneId)
        => ConvertToLocal(utc, timezoneId).ToString("ddd dd MMM yyyy", CultureInfo.InvariantCulture);

    private static string FormatLocalTimeRange(DateTime startUtc, DateTime endUtc, string? timezoneId)
    {
        var start = ConvertToLocal(startUtc, timezoneId);
        var end = ConvertToLocal(endUtc, timezoneId);
        var suffix = string.IsNullOrWhiteSpace(timezoneId) ? "UTC" : timezoneId.Trim();
        return $"{start:HH:mm}-{end:HH:mm} ({suffix})";
    }

    private static DateTime ConvertToLocal(DateTime utc, string? timezoneId)
    {
        var specifiedUtc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        var tz = string.IsNullOrWhiteSpace(timezoneId) ? "UTC" : timezoneId.Trim();

        try
        {
            if (tz.Equals("UTC", StringComparison.OrdinalIgnoreCase))
                return specifiedUtc.ToUniversalTime();

            return TimeZoneInfo.ConvertTimeFromUtc(specifiedUtc, TimeZoneInfo.FindSystemTimeZoneById(tz));
        }
        catch
        {
            return specifiedUtc.ToUniversalTime();
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        var totalMinutes = (int)Math.Round(duration.TotalMinutes);
        if (totalMinutes <= 0)
            return string.Empty;

        return totalMinutes == 1 ? "1 minute" : $"{totalMinutes} minutes";
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
