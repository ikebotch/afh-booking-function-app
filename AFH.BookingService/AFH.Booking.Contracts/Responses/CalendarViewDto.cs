using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Booking.Contracts.Responses;

public sealed class CalendarViewDto
{
    public string AdviserId { get; init; } = default!;

    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }

    public IReadOnlyList<CalendarEventDto> Events { get; init; }
        = Array.Empty<CalendarEventDto>();

    public bool HasConflicts => Events.Any(e => e.IsBusy);
}