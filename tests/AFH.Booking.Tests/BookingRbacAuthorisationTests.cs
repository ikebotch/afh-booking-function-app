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
    public async Task AdviserUserContextClient_CallsLocationIdentityEndpointWithInternalAndUserTokens()
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
                        "adviserId": "adv-1",
                        "jobRole": "Financial Adviser",
                        "roles": ["LeadTech"],
                        "permissions": ["Bookings.Cancel.AsLeadTech"]
                      }
                    }
                    """, Encoding.UTF8, "application/json")
            };
        });

        var sut = new AdviserUserContextClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://location.example") },
            Options.Create(new LocationServiceOptions
            {
                BaseUrl = "https://location.example",
                FunctionKey = "location-key",
                InternalToken = "internal-token"
            }),
            new InternalBearerServiceAuthenticator(),
            NullLogger<AdviserUserContextClient>.Instance);

        var user = await sut.GetCurrentUserAsync("entra-token", CancellationToken.None);

        Assert.NotNull(user);
        Assert.Equal("user-1", user!.UserId);
        Assert.Equal("alex@afh.co.uk", user.Email);
        Assert.Equal("adv-1", user.AdviserId);
        Assert.Equal("Financial Adviser", user.JobRole);
        Assert.Contains(BookingPermissionNames.CancelAsLeadTech, user.Permissions);
        Assert.NotNull(captured);
        Assert.Equal("/api/internal/identity/v1/me", captured!.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", captured.Headers.Authorization?.Scheme);
        Assert.Equal("internal-token", captured.Headers.Authorization?.Parameter);
        Assert.True(captured.Headers.TryGetValues("x-afh-user-token", out var userTokens));
        Assert.Equal("entra-token", Assert.Single(userTokens));
        Assert.True(captured.Headers.TryGetValues("x-functions-key", out var functionKeys));
        Assert.Equal("location-key", Assert.Single(functionKeys));
    }

    [Fact]
    public async Task BookingIdentityAdminClient_CallsLocationIdentityAdminEndpointWithInternalToken()
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
                      "data": [
                        {
                          "permissionId": "00000000-0000-0000-0000-000000000001",
                          "permission": "Bookings.Admin.Read",
                          "displayName": "Read booking admin data",
                          "category": "Bookings",
                          "isEnabled": true
                        }
                      ]
                    }
                    """, Encoding.UTF8, "application/json")
            };
        });

        var sut = new BookingIdentityAdminClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://location.example") },
            Options.Create(new LocationServiceOptions
            {
                BaseUrl = "https://location.example",
                FunctionKey = "location-key",
                InternalToken = "internal-token"
            }),
            new InternalBearerServiceAuthenticator(),
            NullLogger<BookingIdentityAdminClient>.Instance);

        var permissions = await sut.GetAsync<IReadOnlyList<IdentityPermissionStub>>("permissions", CancellationToken.None);

        var permission = Assert.Single(permissions!);
        Assert.Equal("Bookings.Admin.Read", permission.Permission);
        Assert.Equal("/api/internal/identity/v1/permissions", captured!.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", captured.Headers.Authorization?.Scheme);
        Assert.Equal("internal-token", captured.Headers.Authorization?.Parameter);
        Assert.True(captured.Headers.TryGetValues("x-functions-key", out var functionKeys));
        Assert.Equal("location-key", Assert.Single(functionKeys));
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
            new StubPermissionClient(true),
            CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal(HttpStatusCode.Unauthorized, result.FailureResponse?.StatusCode);
    }

    [Fact]
    public async Task DomainUserAccessAuthorizer_ForwardsBearerTokenToLocationIdentityForPermissionDecision()
    {
        var request = TestHttpRequestData.Create();
        request.Headers.Add("Authorization", "Bearer opaque-token");
        var permissions = new CapturingPermissionClient(false);

        var result = await DomainUserAccessAuthorizer.AuthorizeAsync(
            request,
            new EndpointAccessRequirement(EndpointAccessPolicy.UserAuthenticated, BookingPermissionNames.ApprovalsRead),
            permissions,
            CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal(HttpStatusCode.Forbidden, result.FailureResponse?.StatusCode);
        Assert.Equal("opaque-token", permissions.LastBearerToken);
    }

    [Fact]
    public async Task DomainUserAccessAuthorizer_ValidTokenMissingPermissionReturnsForbidden()
    {
        var request = TestHttpRequestData.Create();
        request.Headers.Add("Authorization", "Bearer good-token");

        var result = await DomainUserAccessAuthorizer.AuthorizeAsync(
            request,
            new EndpointAccessRequirement(EndpointAccessPolicy.UserAuthenticated, BookingPermissionNames.ApprovalsReview),
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

    private sealed class CapturingPermissionClient(bool allow) : ICurrentUserPermissionClient
    {
        public string? LastBearerToken { get; private set; }

        public Task<CurrentUserPermissionResult> AuthorizeAsync(string bearerToken, string requiredPermission, CancellationToken ct)
        {
            LastBearerToken = bearerToken;
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

    private sealed class IdentityPermissionStub
    {
        public Guid PermissionId { get; init; }
        public string Permission { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public bool IsEnabled { get; init; }
    }
}
