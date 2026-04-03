using AFH.Booking.Application.Common;
using AFH.Booking.Domain.Options;
using AFH.Booking.Function.Security;
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
    [InlineData("CalendarHealthV1", EndpointAccessPolicy.Public)]
    [InlineData("Booking_OpenApiV1", EndpointAccessPolicy.Public)]
    [InlineData("Bookings_SelfServiceCancel", EndpointAccessPolicy.Public)]
    [InlineData("Users_GetCurrentUser", EndpointAccessPolicy.UserAuthenticated)]
    [InlineData("Bookings_CreateHold", EndpointAccessPolicy.InternalOnly)]
    [InlineData("Admin_GetAdviserProjectionFeed", EndpointAccessPolicy.InternalOnly)]
    [InlineData("Client_GetByTransaction_V2", EndpointAccessPolicy.InternalOnly)]
    public void EndpointAccessPolicies_ClassifiesFunctions(string functionName, EndpointAccessPolicy expected)
    {
        Assert.Equal(expected, EndpointAccessPolicies.GetPolicy(functionName));
    }
}
