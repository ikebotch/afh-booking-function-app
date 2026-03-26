using AFH.Booking.Domain.Auth;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace AFH.Booking.Tests;

public sealed class DomainUserProfileResolverTests
{
    [Fact]
    public void Resolve_MapsConfiguredRoles_AndDerivedCapabilities()
    {
        var resolver = new DomainUserProfileResolver(Options.Create(new DomainUserAuthOptions
        {
            RoleMappings =
            [
                new DomainRoleMappingOptions
                {
                    Role = DomainUserRoles.Manager,
                    Groups = ["manager-group"]
                }
            ]
        }));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("oid", "user-1"),
            new Claim("preferred_username", "alex@afh.co.uk"),
            new Claim("name", "Alex Example"),
            new Claim("roles", DomainUserRoles.Adviser),
            new Claim("groups", "manager-group")
        ], "Test"));

        var profile = resolver.Resolve(principal);

        Assert.Equal("user-1", profile.UserId);
        Assert.Equal("alex@afh.co.uk", profile.Email);
        Assert.Equal(2, profile.Roles.Count);
        Assert.Contains(DomainUserRoles.Adviser, profile.Roles);
        Assert.Contains(DomainUserRoles.Manager, profile.Roles);
        Assert.Contains(DomainUserCapabilities.BookingChangeRequest, profile.Capabilities);
        Assert.Contains(DomainUserCapabilities.BookingChangeApprove, profile.Capabilities);
    }

    [Fact]
    public void Resolve_UsesEmailAndDomainMappings_WhenAppRoleIsNotPresent()
    {
        var resolver = new DomainUserProfileResolver(Options.Create(new DomainUserAuthOptions
        {
            RoleMappings =
            [
                new DomainRoleMappingOptions
                {
                    Role = DomainUserRoles.Operations,
                    Domains = ["afh.co.uk"]
                },
                new DomainRoleMappingOptions
                {
                    Role = DomainUserRoles.Admin,
                    Emails = ["sam.admin@afh.co.uk"]
                }
            ]
        }));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("oid", "user-2"),
            new Claim("preferred_username", "sam.admin@afh.co.uk"),
            new Claim("name", "Sam Admin")
        ], "Test"));

        var profile = resolver.Resolve(principal);

        Assert.Contains(DomainUserRoles.Operations, profile.Roles);
        Assert.Contains(DomainUserRoles.Admin, profile.Roles);
        Assert.Contains(DomainUserCapabilities.BookingAdmin, profile.Capabilities);
    }
}
