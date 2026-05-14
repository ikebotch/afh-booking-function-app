using AFH.Booking.Application.Abstractions.Auth;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AFH.Booking.Infrastructure.Auth;

public sealed class EntraTokenValidator : IEntraTokenValidator
{
    private readonly DomainUserAuthOptions _options;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public EntraTokenValidator(IOptions<DomainUserAuthOptions> options)
        : this(
            options,
            CreateConfigurationManager(options.Value))
    {
    }

    public EntraTokenValidator(
        IOptions<DomainUserAuthOptions> options,
        IConfigurationManager<OpenIdConnectConfiguration> configurationManager)
    {
        _options = options.Value;
        _configurationManager = configurationManager;
    }

    public async Task<DomainUserTokenValidationResult> ValidateAsync(string token, CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            return DomainUserTokenValidationResult.Fail("Domain user authentication is disabled.", "ServerError");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return DomainUserTokenValidationResult.Fail("Bearer token is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.Audience))
        {
            return DomainUserTokenValidationResult.Fail("Domain user auth audience is not configured.", "ServerError");
        }

        try
        {
            var configuration = await _configurationManager.GetConfigurationAsync(ct);
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidAudiences = [_options.Audience],
                ValidIssuers = configuration.Issuer is null ? null : [configuration.Issuer],
                IssuerSigningKeys = configuration.SigningKeys,
                NameClaimType = "name",
                RoleClaimType = "roles",
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out _);

            var tenantId = GetTenantId(principal);
            if (_options.AllowedTenantIds.Count > 0
                && !_options.AllowedTenantIds.Contains(tenantId, StringComparer.OrdinalIgnoreCase))
            {
                return DomainUserTokenValidationResult.Fail("Tenant is not allowed.", "Forbidden");
            }

            var email = GetEmail(principal);

            if (string.IsNullOrWhiteSpace(email))
            {
                return DomainUserTokenValidationResult.Fail("A domain user email claim is required.", "Forbidden");
            }

            if (_options.AllowedEmailDomains.Count > 0)
            {
                var domain = email.Contains('@') ? email[(email.LastIndexOf('@') + 1)..] : string.Empty;
                if (!_options.AllowedEmailDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
                {
                    return DomainUserTokenValidationResult.Fail("Email domain is not allowed.", "Forbidden");
                }
            }

            return DomainUserTokenValidationResult.Success(principal);
        }
        catch (SecurityTokenException ex)
        {
            return DomainUserTokenValidationResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return DomainUserTokenValidationResult.Fail(ex.Message, "ServerError");
        }
    }

    private static string GetAuthority(DomainUserAuthOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Authority))
        {
            return options.Authority;
        }

        if (string.IsNullOrWhiteSpace(options.TenantId))
        {
            throw new InvalidOperationException($"{DomainUserAuthOptions.SectionName}:TenantId is required.");
        }

        return $"https://login.microsoftonline.com/{options.TenantId}/v2.0";
    }

    private static IConfigurationManager<OpenIdConnectConfiguration> CreateConfigurationManager(DomainUserAuthOptions options)
    {
        var authority = GetAuthority(options);
        var metadataAddress = $"{authority.TrimEnd('/')}/.well-known/openid-configuration";
        return new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = options.RequireHttpsMetadata });
    }

    private static string GetTenantId(ClaimsPrincipal principal) =>
        GetClaimValue(principal, "tid", "http://schemas.microsoft.com/identity/claims/tenantid")
        ?? string.Empty;

    private static string GetEmail(ClaimsPrincipal principal) =>
        GetClaimValue(
            principal,
            ClaimTypes.Upn,
            "preferred_username",
            "upn",
            ClaimTypes.Email,
            "email")
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
