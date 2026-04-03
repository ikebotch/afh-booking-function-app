using AFH.BackendPlatform;

namespace AFH.Booking.Function.Security;

public static class EndpointAccessPolicies
{
    private static readonly HashSet<string> PublicFunctions =
    [
        "Booking_OpenApiV1",
        "Booking_ScalarUi",
        "CalendarHealthV1",
        "Bookings_SelfServiceCancel",
        "Bookings_SelfServiceRearrange",
        "Bookings_SelfServiceRearrangementOptions"
    ];

    private static readonly HashSet<string> UserAuthenticatedFunctions =
    [
        "Users_GetCurrentUser"
    ];

    public static EndpointAccessPolicy GetPolicy(string functionName)
    {
        if (PublicFunctions.Contains(functionName))
            return EndpointAccessPolicy.Public;

        if (UserAuthenticatedFunctions.Contains(functionName))
            return EndpointAccessPolicy.UserAuthenticated;

        return EndpointAccessPolicy.InternalOnly;
    }
}
