using AFH.Booking.Application.Abstractions.Auth;
using AFH.Booking.Application.Models.Auth;
using AFH.Booking.Domain;
using AFH.Booking.Domain.Auth;
using AFH.Booking.Domain.Options;
using AFH.Booking.Function.Security;
using AFH.Booking.Infrastructure.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace AFH.Booking.Tests;

public sealed class BookingRbacAuthorisationTests
{
    [Fact]
    public async Task CurrentUserPermissionClient_AllowsUserWithRequiredAdviserPermission()
    {
        var client = new CurrentUserPermissionClient(new StubAdviserUserContextClient(new AdviserUserContext
        {
            Email = "leadtech@afh.co.uk",
            Permissions = [BookingPermissionNames.CancelAsLeadTech]
        }));

        var result = await client.AuthorizeAsync("token", BookingPermissionNames.CancelAsLeadTech, CancellationToken.None);

        Assert.True(result.IsAuthorised);
        Assert.Equal("leadtech@afh.co.uk", result.User?.Email);
    }

    [Fact]
    public async Task CurrentUserPermissionClient_DeniesUserMissingRequiredAdviserPermission()
    {
        var client = new CurrentUserPermissionClient(new StubAdviserUserContextClient(new AdviserUserContext
        {
            Email = "manager@afh.co.uk",
            Permissions = [BookingPermissionNames.ApprovalsRead]
        }));

        var result = await client.AuthorizeAsync("token", BookingPermissionNames.ApprovalsReview, CancellationToken.None);

        Assert.False(result.IsAuthorised);
        Assert.Equal("manager@afh.co.uk", result.User?.Email);
        Assert.Contains(BookingPermissionNames.ApprovalsReview, result.FailureMessage);
    }

    [Fact]
    public async Task AdviserUserContextClient_CallsAdviserBackedCurrentUserEndpointWithBearerToken()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "success": true,
                      "data": {
                        "userId": "user-1",
                        "email": "alex@afh.co.uk",
                        "displayName": "Alex Example",
                        "roles": ["LeadTech"],
                        "permissions": ["Bookings.Cancel.AsLeadTech"]
                      }
                    }
                    """, Encoding.UTF8, "application/json")
            };
        });

        var sut = new AdviserUserContextClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://location.example") },
            Options.Create(new LocationServiceOptions { BaseUrl = "https://location.example" }),
            NullLogger<AdviserUserContextClient>.Instance);

        var user = await sut.GetCurrentUserAsync("entra-token", CancellationToken.None);

        Assert.NotNull(user);
        Assert.Equal("user-1", user!.UserId);
        Assert.Equal("alex@afh.co.uk", user.Email);
        Assert.Contains(BookingPermissionNames.CancelAsLeadTech, user.Permissions);
        Assert.NotNull(captured);
        Assert.Equal("/api/v1/me", captured!.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", captured.Headers.Authorization?.Scheme);
        Assert.Equal("entra-token", captured.Headers.Authorization?.Parameter);
    }

    [Fact]
    public void DomainUserAuthOptions_NoLongerDefinesBookingRoleMappings()
    {
        Assert.DoesNotContain(
            typeof(DomainUserAuthOptions).GetProperties(),
            property => string.Equals(property.Name, "RoleMappings", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DomainUserAccessAuthorizer_MissingBearerTokenReturnsUnauthorized()
    {
        var request = TestHttpRequestData.Create();

        var result = await DomainUserAccessAuthorizer.AuthorizeAsync(
            request,
            new EndpointAccessRequirement(EndpointAccessPolicy.UserAuthenticated, BookingPermissionNames.ApprovalsRead),
            new StubTokenValidator(DomainUserTokenValidationResult.Success(CreatePrincipal())),
            new StubPermissionClient(true),
            CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal(HttpStatusCode.Unauthorized, result.FailureResponse?.StatusCode);
    }

    [Fact]
    public async Task DomainUserAccessAuthorizer_InvalidBearerTokenReturnsUnauthorized()
    {
        var request = TestHttpRequestData.Create();
        request.Headers.Add("Authorization", "Bearer bad-token");

        var result = await DomainUserAccessAuthorizer.AuthorizeAsync(
            request,
            new EndpointAccessRequirement(EndpointAccessPolicy.UserAuthenticated, BookingPermissionNames.ApprovalsRead),
            new StubTokenValidator(DomainUserTokenValidationResult.Fail("Token is invalid.")),
            new StubPermissionClient(true),
            CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal(HttpStatusCode.Unauthorized, result.FailureResponse?.StatusCode);
    }

    [Fact]
    public async Task DomainUserAccessAuthorizer_ValidTokenMissingPermissionReturnsForbidden()
    {
        var request = TestHttpRequestData.Create();
        request.Headers.Add("Authorization", "Bearer good-token");

        var result = await DomainUserAccessAuthorizer.AuthorizeAsync(
            request,
            new EndpointAccessRequirement(EndpointAccessPolicy.UserAuthenticated, BookingPermissionNames.ApprovalsReview),
            new StubTokenValidator(DomainUserTokenValidationResult.Success(CreatePrincipal())),
            new StubPermissionClient(false),
            CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal(HttpStatusCode.Forbidden, result.FailureResponse?.StatusCode);
    }

    [Fact]
    public async Task DomainUserAccessAuthorizer_ValidTokenWithPermissionAllowsEndpoint()
    {
        var request = TestHttpRequestData.Create();
        request.Headers.Add("Authorization", "Bearer good-token");

        var result = await DomainUserAccessAuthorizer.AuthorizeAsync(
            request,
            new EndpointAccessRequirement(EndpointAccessPolicy.UserAuthenticated, BookingPermissionNames.ApprovalsReview),
            new StubTokenValidator(DomainUserTokenValidationResult.Success(CreatePrincipal())),
            new StubPermissionClient(true),
            CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.Equal("alex@afh.co.uk", result.User?.Email);
    }

    [Fact]
    public async Task DomainUserAccessAuthorizer_AuthorisedRequestLoadsAdviserUserContextOnce()
    {
        var request = TestHttpRequestData.Create();
        request.Headers.Add("Authorization", "Bearer good-token");
        var adviser = new CountingAdviserUserContextClient(new AdviserUserContext
        {
            UserId = "user-1",
            Email = "alex@afh.co.uk",
            Permissions = [BookingPermissionNames.ApprovalsRead]
        });
        var permissions = new CurrentUserPermissionClient(adviser);

        var result = await DomainUserAccessAuthorizer.AuthorizeAsync(
            request,
            new EndpointAccessRequirement(EndpointAccessPolicy.UserAuthenticated, BookingPermissionNames.ApprovalsRead),
            new StubTokenValidator(DomainUserTokenValidationResult.Success(CreatePrincipal())),
            permissions,
            CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.Equal(1, adviser.CallCount);
        Assert.Equal("good-token", adviser.LastBearerToken);
    }

    [Fact]
    public async Task CurrentUserPermissionClient_ChecksPermissionLocallyAfterLoadingUserContext()
    {
        var adviser = new CountingAdviserUserContextClient(new AdviserUserContext
        {
            UserId = "user-1",
            Email = "alex@afh.co.uk",
            Roles = ["Manager"],
            Permissions = [BookingPermissionNames.ApprovalsRead]
        });
        var permissions = new CurrentUserPermissionClient(adviser);

        var result = await permissions.AuthorizeAsync("good-token", BookingPermissionNames.ApprovalsReview, CancellationToken.None);

        Assert.False(result.IsAuthorised);
        Assert.Equal(1, adviser.CallCount);
        Assert.Contains(BookingPermissionNames.ApprovalsReview, result.FailureMessage);
    }

    [Fact]
    public async Task CurrentUserPermissionClient_MultiplePermissionChecksInOneScopeReuseAdviserUserContext()
    {
        var adviser = new CountingAdviserUserContextClient(new AdviserUserContext
        {
            UserId = "user-1",
            Email = "alex@afh.co.uk",
            Roles = ["LeadTech"],
            Permissions =
            [
                BookingPermissionNames.CancelAsLeadTech,
                BookingPermissionNames.RearrangementOptionsRead
            ]
        });
        var permissions = new CurrentUserPermissionClient(adviser);

        var cancel = await permissions.AuthorizeAsync("good-token", BookingPermissionNames.CancelAsLeadTech, CancellationToken.None);
        var options = await permissions.AuthorizeAsync("good-token", BookingPermissionNames.RearrangementOptionsRead, CancellationToken.None);

        Assert.True(cancel.IsAuthorised);
        Assert.True(options.IsAuthorised);
        Assert.Equal(1, adviser.CallCount);
    }

    private static ClaimsPrincipal CreatePrincipal() =>
        new(new ClaimsIdentity(
        [
            new Claim("oid", "user-1"),
            new Claim("preferred_username", "alex@afh.co.uk"),
            new Claim("name", "Alex Example")
        ], "Test"));

    private sealed class StubAdviserUserContextClient(AdviserUserContext? user) : IAdviserUserContextClient
    {
        public Task<AdviserUserContext?> GetCurrentUserAsync(string bearerToken, CancellationToken ct)
            => Task.FromResult(user);
    }

    private sealed class CountingAdviserUserContextClient(AdviserUserContext? user) : IAdviserUserContextClient
    {
        public int CallCount { get; private set; }
        public string? LastBearerToken { get; private set; }

        public Task<AdviserUserContext?> GetCurrentUserAsync(string bearerToken, CancellationToken ct)
        {
            CallCount++;
            LastBearerToken = bearerToken;
            return Task.FromResult(user);
        }
    }

    private sealed class StubTokenValidator(DomainUserTokenValidationResult result) : IEntraTokenValidator
    {
        public Task<DomainUserTokenValidationResult> ValidateAsync(string token, CancellationToken ct)
            => Task.FromResult(result);
    }

    private sealed class StubPermissionClient(bool allow) : ICurrentUserPermissionClient
    {
        public Task<CurrentUserPermissionResult> AuthorizeAsync(string bearerToken, string requiredPermission, CancellationToken ct)
        {
            var user = new AdviserUserContext
            {
                UserId = "user-1",
                Email = "alex@afh.co.uk",
                Permissions = allow ? [requiredPermission] : []
            };

            return Task.FromResult(allow
                ? CurrentUserPermissionResult.Authorised(user)
                : CurrentUserPermissionResult.Forbidden(user, requiredPermission));
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handle(request));
    }
}
