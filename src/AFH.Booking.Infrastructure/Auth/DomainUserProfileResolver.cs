using AFH.Booking.Application.Abstractions.Auth;
using AFH.Booking.Domain.Auth;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace AFH.Booking.Infrastructure.Auth;

public sealed class DomainUserProfileResolver : ICurrentUserProfileResolver
{
    private readonly DomainUserAuthOptions _options;

    public DomainUserProfileResolver(IOptions<DomainUserAuthOptions> options)
    {
        _options = options.Value;
    }

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
        var groups = principal.FindAll("groups").Select(claim => claim.Value).ToArray();
        var domain = email.Contains('@') ? email[(email.LastIndexOf('@') + 1)..] : string.Empty;

        foreach (var directRole in directRoles)
        {
            if (DomainUserRoles.Known.Contains(directRole))
            {
                resolvedRoles.Add(directRole);
            }
        }

        foreach (var mapping in _options.RoleMappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.Role))
            {
                continue;
            }

            var match = mapping.AppRoles.Any(appRole => directRoles.Contains(appRole, StringComparer.OrdinalIgnoreCase))
                || mapping.Groups.Any(group => groups.Contains(group, StringComparer.OrdinalIgnoreCase))
                || mapping.Emails.Any(mappedEmail => string.Equals(mappedEmail, email, StringComparison.OrdinalIgnoreCase))
                || mapping.Domains.Any(mappedDomain => string.Equals(mappedDomain, domain, StringComparison.OrdinalIgnoreCase));

            if (match)
            {
                resolvedRoles.Add(mapping.Role);
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
