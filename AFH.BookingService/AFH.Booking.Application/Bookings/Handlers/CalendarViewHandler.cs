using AFH.Booking.Application.Abstractions.Calendar;
using AFH.Booking.Application.Calendar.Queries;
using AFH.Booking.Application.Common;
using AFH.Booking.Contracts.Responses;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Application.Bookings.Handlers;

public sealed class CalendarViewHandler : ICalendarViewHandler
{
    private readonly ICalendarService _calendar;
    private readonly ILogger<CalendarViewHandler> _logger;

    public CalendarViewHandler(ICalendarService calendar, ILogger<CalendarViewHandler> logger)
    {
        _calendar = calendar;
        _logger = logger;
    }

    public async Task<Result<CalendarViewDto>> HandleAsync(CalendarViewQuery query, CancellationToken ct)
    {
        var tasks = query.AdviserIds.Select(async adviserId =>
        {
            try
            {
                var items = await _calendar.GetScheduleAsync(adviserId, query.StartUtc, query.EndUtc, ct);
                return items.Select(i => new CalendarEventDto
                {
                    AdviserId = adviserId,
                    EventId = i.BookingId,
                    Subject = i.Subject,
                    StartUtc = i.StartUtc,
                    EndUtc = i.EndUtc,
                    IsBusy = !string.Equals(i.Status, "Cancelled", StringComparison.OrdinalIgnoreCase),
                    IsCancelled = string.Equals(i.Status, "Cancelled", StringComparison.OrdinalIgnoreCase),
                    IsAllDay = false,
                    Attendees = Array.Empty<string>()
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch calendar schedule for AdviserId={AdviserId}", adviserId);
                return new List<CalendarEventDto>();
            }
        });

        var events = (await Task.WhenAll(tasks))
            .SelectMany(x => x)
            .OrderBy(x => x.StartUtc)
            .ToList();

        _logger.LogInformation("Merged calendar schedules for {Count} advisers", query.AdviserIds.Count);

        return Result<CalendarViewDto>.Ok(new CalendarViewDto
        {
            StartUtc = query.StartUtc,
            EndUtc = query.EndUtc,
            Events = events
        });
    }
}
