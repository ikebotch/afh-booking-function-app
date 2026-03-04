using AFH.Booking.Contracts.Responses;
using AFH.Common.CalendarUtils.Sdk.Contracts.Responses;

namespace AFH.Booking.Application.Calendar.Mapping;

public static class CalendarViewMappings
{
    public static CalendarViewDto ToBookingDto(
        this CalendarViewResponse source)
    {
        return new CalendarViewDto
        {
            AdviserId = source.UserId,
            StartUtc = source.StartUtc,
            EndUtc = source.EndUtc,
            Events = source.Events
                .Select(e => new CalendarEventDto
                {
                    AdviserId = e.UserId,
                    EventId = e.ProviderEventId,
                    Subject = e.Subject,
                    StartUtc = e.StartUtc,
                    EndUtc = e.EndUtc,
                    IsBusy = e.IsBusy,
                    IsAllDay = e.IsAllDay,
                    IsCancelled = e.IsCancelled
                    
                })
                .ToList()
        };
    }
}