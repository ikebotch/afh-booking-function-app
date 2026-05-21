using AFH.Booking.Application.Models.Calendar;
using AFH.Booking.Domain.Calendar;

namespace AFH.Booking.Application.Mapping.Calendar;

public static class CalendarViewMapping
{
    public static CalendarViewDto ToDto(this AdviserAvailabilityResult availability, string adviserId)
    {
        return new CalendarViewDto
        {
            AdviserId = adviserId,
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
        };
    }
}
