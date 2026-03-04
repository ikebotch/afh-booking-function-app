namespace AFH.Booking.Application.Calendar.Mapping;



internal static class CalendarEnumMapper
{
    public static AFH.Common.CalendarUtils.Contracts.Enums.MeetingMode ToCalendar(
        AFH.Booking.Contracts.MeetingMode mode)
        => mode switch
        {
            AFH.Booking.Contracts.MeetingMode.Remote =>
                AFH.Common.CalendarUtils.Contracts.Enums.MeetingMode.Remote,

            AFH.Booking.Contracts.MeetingMode.InPerson =>
                AFH.Common.CalendarUtils.Contracts.Enums.MeetingMode.InPerson,

            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
}