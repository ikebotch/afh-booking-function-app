using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Booking.Domain.Calendar;

public sealed class CalendarViewResult
{
    public IReadOnlyList<AdviserCalendarWindow> Advisers { get; init; } = [];
}