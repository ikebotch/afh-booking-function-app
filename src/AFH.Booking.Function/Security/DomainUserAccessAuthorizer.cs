using AFH.Booking.Application.Abstractions.Auth;
using AFH.Booking.Application.Models.Auth;
using AFH.Booking.Domain;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker.Http;
using System.Security.Claims;

namespace AFH.Booking.Function.Security;

public static class DomainUserAccessAuthorizer
{
    public static async Task<DomainUserAccessResult> AuthorizeAsync(
        HttpRequestData request,
        EndpointAccessRequirement requirement,
        ICurrentUserPermissionClient permissions,
        CancellationToken ct)
    {
        if (requirement.Policy is not EndpointAccessPolicy.UserAuthenticated)
            return DomainUserAccessResult.Allowed(null, null);

        if (!request.Headers.TryGetValues("Authorization", out var authHeaders))
        {
            return DomainUserAccessResult.Denied(await request.ProblemAsync(
                HttpStatusCode.Unauthorized,
                "Missing Authorization header.",
                ct,
                Errors.Unauthorized),
                requiredPermission: requirement.RequiredPermissionDisplay,
                authorised: false);
        }

        var authHeader = authHeaders.FirstOrDefault()?.Trim() ?? string.Empty;
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return DomainUserAccessResult.Denied(await request.ProblemAsync(
                HttpStatusCode.Unauthorized,
                "Authorization header must use Bearer.",
                ct,
                Errors.Unauthorized),
                requiredPermission: requirement.RequiredPermissionDisplay,
                authorised: false);
        }

        var token = authHeader["Bearer ".Length..].Trim();

        if (requirement.RequiredPermissions.Count == 0)
        {
            var currentUser = await permissions.GetCurrentUserAsync(token, ct);
            if (!currentUser.IsAuthorised)
            {
                return DomainUserAccessResult.Denied(
                    await request.ProblemAsync(
                        HttpStatusCode.Unauthorized,
                        currentUser.FailureMessage ?? "Unable to resolve current user.",
                        ct,
                        Errors.Unauthorized),
                    null,
                    currentUser.User,
                    requirement.RequiredPermissionDisplay,
                    authorised: false);
            }

            return DomainUserAccessResult.Allowed(null, currentUser.User);
        }

        if (requirement.RequiredPermissions.Count > 1)
        {
            var currentUser = await permissions.GetCurrentUserAsync(token, ct);
            if (!currentUser.IsAuthorised || currentUser.User is null)
            {
                return DomainUserAccessResult.Denied(
                    await request.ProblemAsync(
                        HttpStatusCode.Unauthorized,
                        currentUser.FailureMessage ?? "Unable to resolve current user.",
                        ct,
                        Errors.Unauthorized),
                    null,
                    currentUser.User,
                    requirement.RequiredPermissionDisplay,
                    authorised: false);
            }

            if (!HasAnyPermission(currentUser.User, requirement.RequiredPermissions))
            {
                return DomainUserAccessResult.Denied(
                    await request.ProblemAsync(
                        HttpStatusCode.Forbidden,
                        $"One of these permissions is required: {requirement.RequiredPermissionDisplay}.",
                        ct,
                        Errors.Forbidden),
                    null,
                    currentUser.User,
                    requirement.RequiredPermissionDisplay,
                    authorised: false);
            }

            return DomainUserAccessResult.Allowed(null, currentUser.User, requirement.RequiredPermissionDisplay);
        }

        var requiredPermission = requirement.RequiredPermissions[0];
        var authorisation = await permissions.AuthorizeAsync(token, requiredPermission, ct);
        if (!authorisation.IsAuthorised)
        {
            return DomainUserAccessResult.Denied(
                await request.ProblemAsync(
                    HttpStatusCode.Forbidden,
                    authorisation.FailureMessage ?? $"Permission '{requiredPermission}' is required.",
                    ct,
                    Errors.Forbidden),
                null,
                authorisation.User,
                requiredPermission,
                authorised: false);
        }

        return DomainUserAccessResult.Allowed(null, authorisation.User, requiredPermission);
    }

    private static bool HasAnyPermission(AdviserUserContext user, IReadOnlyList<string> requiredPermissions)
    {
        return user.Permissions.Contains("*", StringComparer.OrdinalIgnoreCase)
            || requiredPermissions.Any(permission => user.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase));
    }
}

public sealed record DomainUserAccessResult(
    bool IsAllowed,
    ClaimsPrincipal? Principal,
    AdviserUserContext? User,
    HttpResponseData? FailureResponse,
    string? RequiredPermission,
    bool? Authorised)
{
    public static DomainUserAccessResult Allowed(
        ClaimsPrincipal? principal,
        AdviserUserContext? user,
        string? requiredPermission = null) =>
        new(true, principal, user, null, requiredPermission, requiredPermission is null ? null : true);

    public static DomainUserAccessResult Denied(
        HttpResponseData failureResponse,
        ClaimsPrincipal? principal = null,
        AdviserUserContext? user = null,
        string? requiredPermission = null,
        bool? authorised = null) =>
        new(false, principal, user, failureResponse, requiredPermission, authorised);
}
