using AFH.Booking.Application.Models.Auth;
using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Auth;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Security.Claims;

namespace AFH.Booking.Function.Functions.V1.Bookings;

internal static class BookingFunctionActorContext
{
    public static async Task<BookingFunctionUserContextResult> BuildAuthenticatedAsync(
        HttpRequestData req,
        FunctionContext context,
        CancellationToken ct)
    {
        var user = context.GetDomainUserContext();
        if (user is null)
        {
            return BookingFunctionUserContextResult.Fail(
                await req.ProblemAsync(HttpStatusCode.Unauthorized, "Authenticated domain user identity is required.", ct, "Unauthorized"));
        }

        return BookingFunctionUserContextResult.Ok(user);
    }

    public static async Task<BookingFunctionActorContextResult> BuildManagerOrAdminAsync(
        HttpRequestData req,
        FunctionContext context,
        string requiredPermission,
        CancellationToken ct)
    {
        var user = context.GetDomainUserContext();
        if (user is null)
        {
            return BookingFunctionActorContextResult.Fail(
                await req.ProblemAsync(HttpStatusCode.Unauthorized, "Authenticated manager/admin identity is required.", ct, "Unauthorized"));
        }

        if (!user.Permissions.Contains(requiredPermission, StringComparer.OrdinalIgnoreCase))
        {
            return BookingFunctionActorContextResult.Fail(
                await req.ProblemAsync(HttpStatusCode.Forbidden, $"Permission '{requiredPermission}' is required.", ct, "Forbidden"));
        }

        if (string.IsNullOrWhiteSpace(user.UserId) && string.IsNullOrWhiteSpace(user.Email))
        {
            return BookingFunctionActorContextResult.Fail(
                await req.ProblemAsync(HttpStatusCode.Forbidden, "Authenticated manager/admin identity could not be resolved.", ct, "Forbidden"));
        }

        var principal = context.GetDomainUserPrincipal();
        var actorId = FirstNonBlank(
            user.UserId,
            GetClaimValue(principal, "oid", "http://schemas.microsoft.com/identity/claims/objectidentifier", ClaimTypes.NameIdentifier),
            user.Email,
            GetClaimValue(principal, ClaimTypes.Email, "email", ClaimTypes.Upn, "preferred_username"));
        var displayName = FirstNonBlank(user.DisplayName, GetClaimValue(principal, "name", ClaimTypes.Name));
        var correlationId = BookingChangeRequestContext.GetCorrelationId(req);

        var actor = IsManager(user)
            ? BookingActorContext.ManagerPortal(actorId, displayName, correlationId, user.Permissions)
            : BookingActorContext.InternalAdmin(actorId, displayName, correlationId, canOverrideRules: true, user.Permissions);

        return BookingFunctionActorContextResult.Ok(actor);
    }

    public static async Task<HttpResponseData?> EnsureCanAccessBookingAsync(
        HttpRequestData req,
        AdviserUserContext user,
        BookingDetailsResponse booking,
        CancellationToken ct)
    {
        if (CanAccessBooking(user, booking.AdviserId))
            return null;

        return await req.ProblemAsync(
            HttpStatusCode.Forbidden,
            "Signed-in user can only access bookings for their mapped adviser unless they have booking admin access.",
            ct,
            "Forbidden");
    }

    public static bool CanAccessBooking(AdviserUserContext user, string adviserId)
    {
        if (user.Permissions.Contains(Domain.Auth.BookingPermissionNames.AdminRead, StringComparer.OrdinalIgnoreCase))
            return true;

        return !string.IsNullOrWhiteSpace(user.AdviserId)
            && string.Equals(user.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManager(AdviserUserContext user)
        => user.Roles.Any(role =>
            role.Equals(BookingActorContext.ActorManager, StringComparison.OrdinalIgnoreCase) ||
            role.Equals("OperationsManager", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("ReportingManager", StringComparison.OrdinalIgnoreCase));

    private static string? GetClaimValue(ClaimsPrincipal? principal, params string[] claimTypes)
    {
        if (principal is null)
            return null;

        foreach (var claimType in claimTypes)
        {
            var claim = principal.FindFirst(claimType);
            if (!string.IsNullOrWhiteSpace(claim?.Value))
                return claim.Value;
        }

        return null;
    }

    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}

internal sealed record BookingFunctionActorContextResult(
    bool IsSuccess,
    BookingActorContext? ActorContext,
    HttpResponseData? Response)
{
    public static BookingFunctionActorContextResult Ok(BookingActorContext actor)
        => new(true, actor, null);

    public static BookingFunctionActorContextResult Fail(HttpResponseData response)
        => new(false, null, response);
}

internal sealed record BookingFunctionUserContextResult(
    bool IsSuccess,
    AdviserUserContext? User,
    HttpResponseData? Response)
{
    public static BookingFunctionUserContextResult Ok(AdviserUserContext user)
        => new(true, user, null);

    public static BookingFunctionUserContextResult Fail(HttpResponseData response)
        => new(false, null, response);
}
