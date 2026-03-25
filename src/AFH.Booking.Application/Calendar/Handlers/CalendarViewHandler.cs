using AFH.Booking.Contracts.V1.Dtos;
using AFH.Booking.Domain.Calendar;

namespace AFH.Booking.Application.Calendar.Queries;

public sealed class CalendarViewQueryHandler : ICalendarViewQueryHandler
{
    private readonly ICalendarGateway _calendar;

    public CalendarViewQueryHandler(ICalendarGateway calendar)
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
                freshnessMode: "PreferCached",
                ct: ct);

            items.Add(new CalendarViewDto
            {
                AdviserId = u.Email,
                IsBusy = !availability.IsFree,
                MailboxUnavailable = availability.MailboxUnavailable,
                Message = availability.StatusMessage,
                Conflicts = availability.Conflicts
                    .Select(c => new CalendarBlock
                    {
                        StartUtc = c.StartUtc,
                        EndUtc = c.EndUtc,
                        Subject = c.Subject
                    })
                    .OrderBy(x => x.StartUtc)
                    .ToList()
            });
        }






        return Result<List<CalendarViewDto>>.Ok(items);
    }
}
