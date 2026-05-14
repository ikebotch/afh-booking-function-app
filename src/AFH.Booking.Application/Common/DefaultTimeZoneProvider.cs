using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Application.Common;

public sealed class DefaultTimeZoneProvider : ITimeZoneProvider
{
    private readonly CalendarOptions _options;

    public DefaultTimeZoneProvider(IOptions<CalendarOptions> options)
    {
        _options = options.Value;
    }

    public string DefaultTimeZoneId =>
        string.IsNullOrWhiteSpace(_options.DefaultTimezone)
            ? "UTC"
            : _options.DefaultTimezone.Trim();
}
