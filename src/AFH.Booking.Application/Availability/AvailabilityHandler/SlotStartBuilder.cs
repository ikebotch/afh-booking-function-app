using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Domain.Availability;

namespace AFH.Booking.Application.Availability;

public sealed class SlotStartBuilder : ISlotStartBuilder
{
    private static readonly TimeSpan DefaultDayStart = TimeSpan.FromHours(8);
    private static readonly TimeSpan DefaultDayEnd = TimeSpan.FromHours(17);

    public (IReadOnlyList<DateTime> Starts, string? NextCursor) BuildPage(GetAvailabilityQuery query)
    {
        var duration = TimeSpan.FromMinutes(query.Duration);
        var take = query.Take <= 0 ? 10 : Math.Min(query.Take, 100);

        DateTime? cursor = null;
        if (!string.IsNullOrWhiteSpace(query.Cursor) &&
            DateTime.TryParse(query.Cursor, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var c))
        {
            cursor = DateTime.SpecifyKind(c, DateTimeKind.Utc);
        }

        static bool AfterCursor(DateTime candidate, DateTime? cur)
            => cur is null || candidate > cur.Value;

        var result = new List<DateTime>(take);

        var preferred = DateTime.SpecifyKind(query.PreferredStart, DateTimeKind.Utc);

        var start = preferred.TimeOfDay == TimeSpan.Zero
            ? preferred.Date.Add(DefaultDayStart)
            : preferred;

        var end = preferred.TimeOfDay == TimeSpan.Zero
            ? preferred.Date.Add(DefaultDayEnd)
            : preferred.Add(duration);

        for (var t = start; t.Add(duration) <= end; t = t.Add(duration))
        {
            if (!AfterCursor(t, cursor))
                continue;

            result.Add(t);

            if (result.Count == take)
                return (result, t.Add(duration).ToString("O"));
        }

        return (result, null);
    }
}
