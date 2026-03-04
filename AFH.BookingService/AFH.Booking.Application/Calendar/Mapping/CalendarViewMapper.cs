using AFH.Booking.Contracts.Responses;
using AFH.Common.CalendarUtils.Sdk.Contracts.Responses;

namespace AFH.Booking.Application.Calendar.Mapping;

public static class CalendarViewMapper
{
    public static CalendarViewDto Merge(
        IEnumerable<CalendarViewResponse> responses)
    {
        var events = responses
            .SelectMany(r => r.Events)
            .Select(e => new CalendarEventDto
            {
                
                AdviserId = e.UserId,
                Subject = e.Subject,
                StartUtc = e.StartUtc,
                EndUtc = e.EndUtc,
                IsBusy = e.IsBusy,
                EventId = e.EventId,
                Attendees = e.Attendees.Select(c=> c.Email).ToList() ?? []
                
            })
            .OrderBy(e => e.StartUtc)
            .ToList();

        return new CalendarViewDto
        {
            Events = events
        };
    }
}