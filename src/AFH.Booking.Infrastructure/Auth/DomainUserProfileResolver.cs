using AFH.Booking.Application.Abstractions.Auth;
using AFH.Booking.Domain.Auth;
using System.Security.Claims;

namespace AFH.Booking.Infrastructure.Auth;

public sealed class DomainUserProfileResolver : ICurrentUserProfileResolver
{
    public CurrentUserProfile Resolve(ClaimsPrincipal principal)
    {
        var email = GetEmail(principal);
        var roles = ResolveRoles(principal, email);
        var capabilities = DomainUserCapabilities.ForRoles(roles);

        return new CurrentUserProfile
        {
            UserId = GetClaimValue(
                principal,
                "oid",
                "http://schemas.microsoft.com/identity/claims/objectidentifier",
                ClaimTypes.NameIdentifier)
                ?? email,
            Email = email,
            DisplayName = GetClaimValue(principal, "name", ClaimTypes.Name)
                ?? principal.Identity?.Name
                ?? email,
            Roles = roles,
            Capabilities = capabilities
        };
    }

    private IReadOnlyList<string> ResolveRoles(ClaimsPrincipal principal, string email)
    {
        var resolvedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directRoles = principal.FindAll("roles")
            .Concat(principal.FindAll(ClaimTypes.Role))
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var directRole in directRoles)
        {
            if (DomainUserRoles.Known.Contains(directRole))
            {
                resolvedRoles.Add(directRole);
            }
        }

        return resolvedRoles.OrderBy(role => role, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string GetEmail(ClaimsPrincipal principal) =>
        GetClaimValue(principal, ClaimTypes.Upn, "preferred_username", "upn", ClaimTypes.Email, "email")
        ?? string.Empty;

    private static string? GetClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var claim = principal.FindFirst(claimType);
            if (!string.IsNullOrWhiteSpace(claim?.Value))
            {
                return claim.Value;
            }
        }

        return null;
    }
}
