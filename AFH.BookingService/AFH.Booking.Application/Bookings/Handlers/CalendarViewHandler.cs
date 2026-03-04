using AFH.Booking.Application.Calendar.Mapping;
using AFH.Booking.Application.Calendar.Queries;
using AFH.Booking.Application.Common;
using AFH.Booking.Contracts.Responses;
using AFH.Common.CalendarUtils.Sdk.Contracts.Requests;
using AFH.Common.CalendarUtils.Sdk.Services.Abstractions;
using Microsoft.Extensions.Logging;
namespace AFH.Booking.Application.Bookings.Handlers;
public sealed class CalendarViewHandler : ICalendarViewHandler
{
    private readonly ICalendarClient _calendar;
    private readonly ILogger<CalendarViewHandler> _logger;

    public CalendarViewHandler(ICalendarClient calendar, ILogger<CalendarViewHandler> logger)
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
                return await _calendar.GetCalendarViewAsync(
                    new CalendarViewRequest
                    {
                        UserId = adviserId,
                        StartUtc = query.StartUtc,
                        EndUtc = query.EndUtc
                    },
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch calendar view for AdviserId={AdviserId}", adviserId);
                return null;
            }
        });

        var views = (await Task.WhenAll(tasks)).Where(v => v != null);

        var merged = CalendarViewMapper.Merge(views);

        _logger.LogInformation("Merged calendar views for {Count} advisers", query.AdviserIds.Count);

        return Result<CalendarViewDto>.Ok(merged);
    }
}
