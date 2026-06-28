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
        if (CanAccessBooking(user, booking))
            return null;

        return await req.ProblemAsync(
            HttpStatusCode.Forbidden,
            "Signed-in user can only access bookings inside their assigned access scope.",
            ct,
            "Forbidden");
    }

    public static bool CanAccessBooking(AdviserUserContext user, string adviserId)
    {
        if (HasUnrestrictedScope(user, "Bookings"))
            return true;

        return !string.IsNullOrWhiteSpace(user.AdviserId)
            && string.Equals(user.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanAccessBooking(AdviserUserContext user, BookingDetailsResponse booking)
    {
        if (HasUnrestrictedScope(user, "Bookings"))
            return true;

        if (MatchesScopedValue(user, "Bookings", "AdviserSelf", booking.AdviserId)
            || MatchesScopedValue(user, "Bookings", "Adviser", booking.AdviserId))
        {
            return true;
        }

        if (MatchesScopedValue(user, "Bookings", "Region", booking.AdviserRegion))
            return true;

        return MatchesScopedValue(user, "Bookings", "Branch", booking.LocationRef)
            || MatchesScopedValue(user, "Bookings", "Location", booking.LocationRef);
    }

    public static bool HasUnrestrictedScope(AdviserUserContext user, string area)
    {
        if (user.AccessScopes.Any(scope =>
                IsAreaMatch(scope.Area, area)
                && (scope.ScopeType.Equals("All", StringComparison.OrdinalIgnoreCase)
                    || scope.ScopeType.Equals("Organisation", StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        return user.Permissions.Contains("*", StringComparer.OrdinalIgnoreCase)
            || (user.AccessScopes.Count == 0
                && HasLegacyBroadBookingPermission(user))
            || (user.Permissions.Contains(Domain.Auth.BookingPermissionNames.AdminRead, StringComparer.OrdinalIgnoreCase)
                && user.Roles.Any(role =>
                    role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                    || role.Equals("Operations", StringComparison.OrdinalIgnoreCase)));
    }

    public static bool MatchesScopedValue(AdviserUserContext user, string area, string scopeType, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return user.AccessScopes.Any(scope =>
            IsAreaMatch(scope.Area, area)
            && scope.ScopeType.Equals(scopeType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(scope.ScopeValue, value.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAreaMatch(string? scopeArea, string area)
        => string.Equals(scopeArea, "*", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scopeArea, area, StringComparison.OrdinalIgnoreCase);

    private static bool HasLegacyBroadBookingPermission(AdviserUserContext user)
        => user.Permissions.Contains(Domain.Auth.BookingPermissionNames.AdminRead, StringComparer.OrdinalIgnoreCase)
            || user.Permissions.Contains(Domain.Auth.BookingPermissionNames.CancelDirect, StringComparer.OrdinalIgnoreCase)
            || user.Permissions.Contains(Domain.Auth.BookingPermissionNames.RearrangeDirect, StringComparer.OrdinalIgnoreCase)
            || user.Permissions.Contains(Domain.Auth.BookingPermissionNames.CancelAsLeadTech, StringComparer.OrdinalIgnoreCase)
            || user.Permissions.Contains(Domain.Auth.BookingPermissionNames.RearrangeAsLeadTech, StringComparer.OrdinalIgnoreCase);

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
