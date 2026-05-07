using AFH.Booking.Application.Common;
using AFH.BackendPlatform;
using AFH.Booking.Domain.Options;
using AFH.Booking.Function.Functions.V1.Availability;
using AFH.Booking.Function.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using System.Reflection;

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
    [InlineData("Booking_ScalarUi", EndpointAccessPolicy.Public)]
    [InlineData("Bookings_SelfServiceCancel", EndpointAccessPolicy.Public)]
    [InlineData("Bookings_SelfServiceGetBooking", EndpointAccessPolicy.Public)]
    [InlineData("Bookings_SelfServiceRearrange", EndpointAccessPolicy.Public)]
    [InlineData("Bookings_SelfServiceRearrangementOptions", EndpointAccessPolicy.Public)]
    [InlineData("Users_GetCurrentUser", EndpointAccessPolicy.UserAuthenticated)]
    [InlineData("Bookings_CreateClientLink", EndpointAccessPolicy.InternalOnly)]
    [InlineData("Bookings_CreateHold", EndpointAccessPolicy.InternalOnly)]
    [InlineData("Bookings_ResendClientLink", EndpointAccessPolicy.InternalOnly)]
    [InlineData("Admin_GetAdviserProjectionFeed", EndpointAccessPolicy.InternalOnly)]
    [InlineData("Bookings_RecordEmailBounce", EndpointAccessPolicy.InternalOnly)]
    [InlineData("Client_GetByTransaction_V2", EndpointAccessPolicy.InternalOnly)]
    [InlineData("Config_DeleteMeetingType", EndpointAccessPolicy.InternalOnly)]
    [InlineData("Config_DeleteMeetingTopic", EndpointAccessPolicy.InternalOnly)]
    [InlineData("Config_GetMeetingTypes", EndpointAccessPolicy.Public)]
    [InlineData("Config_GetMeetingTopics", EndpointAccessPolicy.Public)]
    [InlineData("Config_UpsertMeetingType", EndpointAccessPolicy.InternalOnly)]
    [InlineData("Config_UpsertMeetingTopic", EndpointAccessPolicy.InternalOnly)]
    public void EndpointAccessPolicies_ClassifiesFunctions(string functionName, EndpointAccessPolicy expected)
    {
        Assert.Equal(expected, EndpointAccessPolicies.GetPolicy(functionName));
    }

    [Fact]
    public void EndpointAccessPolicies_CoversEveryHttpTriggeredFunctionExplicitly()
    {
        var httpFunctionNames = typeof(GetAvailabilityFunction).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .Where(method => method.GetCustomAttribute<FunctionAttribute>() is not null)
            .Where(method => method.GetParameters().Any(parameter => parameter.GetCustomAttributes<HttpTriggerAttribute>(inherit: false).Any()))
            .Select(method => method.GetCustomAttribute<FunctionAttribute>()!.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var configuredNames = EndpointAccessPolicies.KnownHttpFunctions
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(configuredNames, httpFunctionNames);
    }

    [Fact]
    public void EndpointAccessPolicies_ThrowsForUnknownFunction()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => EndpointAccessPolicies.GetPolicy("Unmapped_Http_Function"));

        Assert.Contains("No endpoint access policy is configured", exception.Message);
    }
}
