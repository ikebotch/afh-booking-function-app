using AFH.Booking.Application.Abstractions.Auth;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AFH.Booking.Tests;

public sealed class EntraTokenValidatorTests
{
    private const string Issuer = "https://login.microsoftonline.com/test-tenant/v2.0";
    private static readonly SymmetricSecurityKey SigningKey = new(Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"));

    [Fact]
    public async Task ValidateAsync_ReturnsSuccess_ForValidToken()
    {
        var validator = CreateValidator(new DomainUserAuthOptions
        {
            Enabled = true,
            Audience = "api://booking-api",
            AllowedTenantIds = ["test-tenant"],
            AllowedEmailDomains = ["afh.co.uk"]
        });

        var token = CreateToken(
            audience: "api://booking-api",
            tenantId: "test-tenant",
            email: "alex@afh.co.uk");

        var result = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Principal);
        Assert.Equal("alex@afh.co.uk", result.Principal!.FindFirst("preferred_username")?.Value);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsForbidden_WhenEmailDomainIsNotAllowed()
    {
        var validator = CreateValidator(new DomainUserAuthOptions
        {
            Enabled = true,
            Audience = "api://booking-api",
            AllowedTenantIds = ["test-tenant"],
            AllowedEmailDomains = ["afh.co.uk"]
        });

        var token = CreateToken(
            audience: "api://booking-api",
            tenantId: "test-tenant",
            email: "alex@contoso.com");

        var result = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Forbidden", result.ErrorCode);
        Assert.Contains("Email domain", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsForbidden_WhenEmailClaimIsMissing()
    {
        var validator = CreateValidator(new DomainUserAuthOptions
        {
            Enabled = true,
            Audience = "api://booking-api",
            AllowedTenantIds = ["test-tenant"]
        });

        var token = CreateToken(
            audience: "api://booking-api",
            tenantId: "test-tenant",
            email: null);

        var result = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Forbidden", result.ErrorCode);
        Assert.Contains("email claim", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsUnauthorized_WhenAudienceIsInvalid()
    {
        var validator = CreateValidator(new DomainUserAuthOptions
        {
            Enabled = true,
            Audience = "api://booking-api",
            AllowedTenantIds = ["test-tenant"],
            AllowedEmailDomains = ["afh.co.uk"]
        });

        var token = CreateToken(
            audience: "api://wrong-api",
            tenantId: "test-tenant",
            email: "alex@afh.co.uk");

        var result = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Unauthorized", result.ErrorCode);
    }

    private static EntraTokenValidator CreateValidator(DomainUserAuthOptions options)
    {
        var configuration = new OpenIdConnectConfiguration
        {
            Issuer = Issuer
        };
        configuration.SigningKeys.Add(SigningKey);

        return new EntraTokenValidator(
            Options.Create(options),
            new StaticOpenIdConfigurationManager(configuration));
    }

    private static string CreateToken(
        string audience,
        string tenantId,
        string? email)
    {
        var claims = new List<Claim>
        {
            new("tid", tenantId),
            new("oid", "user-123"),
            new("name", "Alex Example"),
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            claims.Add(new Claim("preferred_username", email));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Audience = audience,
            Issuer = Issuer,
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)
        };

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityTokenHandler().CreateToken(descriptor));
    }

    private sealed class StaticOpenIdConfigurationManager : IConfigurationManager<OpenIdConnectConfiguration>
    {
        private readonly OpenIdConnectConfiguration _configuration;

        public StaticOpenIdConfigurationManager(OpenIdConnectConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel) =>
            Task.FromResult(_configuration);

        public void RequestRefresh()
        {
        }
    }
}
