using AFH.Booking.Domain.Availability;
using System.Globalization;

namespace AFH.Booking.Application.Common;

public static class AvailabilityParsing
{
    public static bool TryParsePreferredStart(string? raw, out PreferredStart preferred)
    {
        preferred = new PreferredStart { Kind = PreferredStartKind.None };

        if (string.IsNullOrWhiteSpace(raw))
            return true;

        // date-only: yyyy-MM-dd
        if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            preferred.Kind = PreferredStartKind.DateOnly;
            preferred.DateUtc = d;
            return true;
        }

        // datetime: ISO with offset/Z
        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            preferred.Kind = PreferredStartKind.DateTimeUtc;
            preferred.StartUtc = DateTime.SpecifyKind(dto.UtcDateTime, DateTimeKind.Utc);
            return true;
        }

        return false;
    }
}