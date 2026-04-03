using AFH.Booking.Application.Common;
using AFH.Booking.Domain.Options;
using AFH.Booking.Function.Middleware;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Tests;

public class TimeZoneAndRouteTests
{
    [Fact]
    public void DefaultTimeZoneProvider_ReturnsConfiguredTimeZone()
    {
        var provider = new DefaultTimeZoneProvider(Options.Create(new CalendarOptions
        {
            DefaultTimezone = "Europe/Belfast"
        }));

        Assert.Equal("Europe/Belfast", provider.DefaultTimeZoneId);
    }

    [Theory]
    [InlineData("/api/v1/calendar/health", true, false)]
    [InlineData("/api/openapi/v1.json", true, false)]
    [InlineData("/api/v1/bookings/hold", false, false)]
    public void InternalApiAuthMiddleware_ClassifiesRoutes(string path, bool isPublic, bool requiresInternalBearer)
    {
        Assert.Equal(isPublic, InternalApiAuthMiddleware.IsPublic(path));
        Assert.Equal(requiresInternalBearer, InternalApiAuthMiddleware.RequiresInternalBearer(path));
    }
}
