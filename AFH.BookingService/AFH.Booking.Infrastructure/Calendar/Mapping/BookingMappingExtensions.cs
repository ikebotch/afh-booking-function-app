using AFH.Common.CalendarUtils.Contracts.Enums;
using AFH.Common.CalendarUtils.Contracts.Requests;

namespace AFH.Booking.Infrastructure.Calendar.Mapping;

public static class BookingMappingExtensions
{
    public static UpsertCalendarEventRequest ToUpsertRequest(this BookingsModel booking)
    {
        return new UpsertCalendarEventRequest
        {
            ExternalId = booking.Id.Value,
            UserId = booking.AdviserId,
            Subject = string.IsNullOrWhiteSpace(booking.Subject) ? "AFH Booking" : booking.Subject,
            StartUtc = booking.StartUtc,
            EndUtc = booking.EndUtc,
            Timezone = booking.Timezone,
            Mode = booking.Mode,
            Kind = CalendarEventKind.Hold,
            Attendees = null,
            Location = null,
            ProviderEventId = booking.ProviderEventId,
            Body = booking.Notes,
            TransactionId = booking.TransactionId,
            IsRemote = booking.Mode == MeetingMode.Remote
        };
    }
}
