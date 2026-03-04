using AFH.Booking.Contracts;
using CalendarMeetingMode = AFH.Common.CalendarUtils.Contracts.Enums.MeetingMode;

namespace AFH.Booking.Application.Calendar.Mapping;

public static class MeetingModeExtensions
{
    public static CalendarMeetingMode ToCalendar(this MeetingMode mode)
        => mode switch
        {
            MeetingMode.Remote => CalendarMeetingMode.Remote,
            MeetingMode.InPerson => CalendarMeetingMode.InPerson,
          

            _ => throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "Unsupported meeting mode for Calendar integration"
            )
        };
}