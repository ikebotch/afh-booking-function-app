using AFH.Booking.Infrastructure.Calendar.Mapping;
using AFH.Common.CalendarUtils.Sdk.Services.Abstractions;

public sealed class CalendarService : ICalendarService
{
    private readonly ICalendarClient _client;

    public CalendarService(ICalendarClient client)
    {
        _client = client;
    }

    public async Task<string> CreateEventAsync(BookingsModel booking, CancellationToken ct)
    {
        var req = booking.ToUpsertRequest(); 
        var result = await _client.UpsertAsync(req, ct);
        return result.ProviderEventId;
    }

    public Task CancelEventAsync(string userId, string providerEventId, CancellationToken ct)
        => _client.CancelAsync(userId, providerEventId, ct);
}
