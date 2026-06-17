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
                requiredPermission: requirement.RequiredPermission,
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
                requiredPermission: requirement.RequiredPermission,
                authorised: false);
        }

        var token = authHeader["Bearer ".Length..].Trim();

        if (string.IsNullOrWhiteSpace(requirement.RequiredPermission))
            return DomainUserAccessResult.Allowed(null, null);

        var authorisation = await permissions.AuthorizeAsync(token, requirement.RequiredPermission, ct);
        if (!authorisation.IsAuthorised)
        {
            return DomainUserAccessResult.Denied(
                await request.ProblemAsync(
                    HttpStatusCode.Forbidden,
                    authorisation.FailureMessage ?? $"Permission '{requirement.RequiredPermission}' is required.",
                    ct,
                    Errors.Forbidden),
                null,
                authorisation.User,
                requirement.RequiredPermission,
                authorised: false);
        }

        return DomainUserAccessResult.Allowed(null, authorisation.User, requirement.RequiredPermission);
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
