using AFH.Booking.Application.Mapping.Calendar;
using AFH.Booking.Application.Models.Calendar;
using AFH.Booking.Domain.Calendar;

namespace AFH.Booking.Application.Calendar;

public sealed class CalendarViewQueryService : ICalendarViewQueryService
{
    private readonly ICalendarGateway _calendar;

    public CalendarViewQueryService(ICalendarGateway calendar)
    {
        _calendar = calendar;
    }


    public async Task<Result<List<CalendarViewDto>>> HandleAsync(CalendarViewQuery q, CancellationToken ct)
    {
        if (q.AdviserList is null || q.AdviserList.Count == 0)
            return Result<List<CalendarViewDto>>.Fail(HttpStatusCode.BadRequest, "users is required.", Errors.Validation);

        if (q.EndUtc <= q.StartUtc)
            return Result<List<CalendarViewDto>>.Fail(HttpStatusCode.BadRequest, "endUtc must be after startUtc.", Errors.Validation);

        var items = new List<CalendarViewDto>(q.AdviserList.Count);

        foreach (var u in q.AdviserList)
        {
            if (string.IsNullOrWhiteSpace(u.Email))
                continue;

            var availability = await _calendar.CheckAvailabilityAsync(
                userId: u.Email!.Trim(),
                startUtc: q.StartUtc,
                endUtc: q.EndUtc,
                timezone: q.Timezone,
                          //freshnessMode: "PreferCached",
                          freshnessMode: "ForceRefresh",
                ct: ct);

            items.Add(availability.ToDto(u.AdviserId));
        }






        return Result<List<CalendarViewDto>>.Ok(items);
    }
}
