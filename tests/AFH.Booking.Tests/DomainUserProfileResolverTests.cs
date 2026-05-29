using AFH.Booking.Domain.Auth;
using AFH.Booking.Infrastructure.Auth;
using System.Security.Claims;

namespace AFH.Booking.Tests;

public sealed class DomainUserProfileResolverTests
{
    [Fact]
    public void Resolve_MapsDirectRoles_AndDerivedCapabilities()
    {
        var resolver = new DomainUserProfileResolver();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("oid", "user-1"),
            new Claim("preferred_username", "alex@afh.co.uk"),
            new Claim("name", "Alex Example"),
            new Claim("roles", DomainUserRoles.Adviser),
            new Claim("roles", DomainUserRoles.Manager)
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
    public void Resolve_DoesNotUseBookingConfigRoleMappings()
    {
        var resolver = new DomainUserProfileResolver();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("oid", "user-2"),
            new Claim("preferred_username", "sam.admin@afh.co.uk"),
            new Claim("name", "Sam Admin")
        ], "Test"));

        var profile = resolver.Resolve(principal);

        Assert.Empty(profile.Roles);
        Assert.Empty(profile.Capabilities);
    }
}
