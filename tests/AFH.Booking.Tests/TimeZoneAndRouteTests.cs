using AFH.Booking.Domain;
using AFH.Booking.Domain.Auth;
using AFH.Booking.Domain.Options;
using AFH.Booking.Function.Functions.V1.Availability;
using AFH.Booking.Function.Security;
using Microsoft.Azure.Functions.Worker;
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
    [InlineData("Bookings_CreateHold", EndpointAccessPolicy.Public)]
    [InlineData("Admin_GetAdviserProjectionFeed", EndpointAccessPolicy.InternalOnly)]
    [InlineData("Bookings_RecordEmailBounce", EndpointAccessPolicy.InternalOnly)]
    [InlineData("Client_GetByTransaction_V2", EndpointAccessPolicy.InternalOnly)]
    [InlineData("Config_DeleteMeetingType", EndpointAccessPolicy.InternalOnly)]
    [InlineData("Config_DeleteMeetingTopic", EndpointAccessPolicy.InternalOnly)]
    [InlineData("Config_GetMeetingTypes", EndpointAccessPolicy.Public)]
    [InlineData("Config_GetMeetingTopics", EndpointAccessPolicy.Public)]
    [InlineData("Config_UpsertMeetingType", EndpointAccessPolicy.InternalOnly)]
    [InlineData("Config_UpsertMeetingTopic", EndpointAccessPolicy.InternalOnly)]
    [InlineData("Approvals_ListPending", EndpointAccessPolicy.UserAuthenticated)]
    [InlineData("Approvals_Review", EndpointAccessPolicy.UserAuthenticated)]
    [InlineData("Approvals_ListAdviserRequests", EndpointAccessPolicy.UserAuthenticated)]
    [InlineData("Bookings_CreateApprovalRequest", EndpointAccessPolicy.UserAuthenticated)]
    [InlineData("Bookings_LeadTechCancel", EndpointAccessPolicy.UserAuthenticated)]
    [InlineData("Bookings_LeadTechRearrange", EndpointAccessPolicy.UserAuthenticated)]
    [InlineData("Bookings_LeadTechRearrangementOptions", EndpointAccessPolicy.UserAuthenticated)]
    public void EndpointAccessPolicies_ClassifiesFunctions(string functionName, EndpointAccessPolicy expected)
    {
        Assert.Equal(expected, EndpointAccessPolicies.GetPolicy(functionName));
    }

    [Theory]
    [InlineData("Approvals_ListPending", BookingPermissionNames.ApprovalsRead)]
    [InlineData("Approvals_Review", BookingPermissionNames.ApprovalsReview)]
    [InlineData("Approvals_ListAdviserRequests", BookingPermissionNames.ApprovalRequestsReadOwn)]
    [InlineData("Bookings_CreateApprovalRequest", BookingPermissionNames.ApprovalRequestsCreate)]
    [InlineData("Bookings_LeadTechCancel", BookingPermissionNames.CancelAsLeadTech)]
    [InlineData("Bookings_LeadTechRearrange", BookingPermissionNames.RearrangeAsLeadTech)]
    [InlineData("Bookings_LeadTechRearrangementOptions", BookingPermissionNames.RearrangementOptionsRead)]
    public void EndpointAccessPolicies_SelectedAdminFunctionsRequireBookingPermissions(string functionName, string expectedPermission)
    {
        var requirement = EndpointAccessPolicies.GetRequirement(functionName);

        Assert.Equal(EndpointAccessPolicy.UserAuthenticated, requirement.Policy);
        Assert.Equal(expectedPermission, requirement.RequiredPermission);
    }

    [Theory]
    [InlineData("Bookings_GetRearrangementOptions")]
    [InlineData("Bookings_Rearrange")]
    [InlineData("Bookings_RecordNoShow")]
    [InlineData("Transactions_Availability_V2")]
    public void EndpointAccessPolicies_ExistingServiceToServiceEndpointsRemainInternalOnly(string functionName)
    {
        var requirement = EndpointAccessPolicies.GetRequirement(functionName);

        Assert.Equal(EndpointAccessPolicy.InternalOnly, requirement.Policy);
        Assert.Null(requirement.RequiredPermission);
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
