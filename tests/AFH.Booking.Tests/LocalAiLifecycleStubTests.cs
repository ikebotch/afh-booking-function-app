using AFH.Booking.Application.Abstractions.Auth;
using AFH.Booking.Application.Models.Auth;
using AFH.Booking.Function.Functions.V1.Bookings;
using AFH.Booking.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AFH.Booking.Tests;

public sealed class LocalAiLifecycleStubTests : IDisposable
{
    private readonly string? _originalAllowMockTokens = Environment.GetEnvironmentVariable("Booking__AllowMockTokens");

    [Fact]
    public void IsEnabled_ReturnsFalseOutsideDevelopmentEvenWhenConfigured()
    {
        var configuration = Configuration(("Booking:AiTools:EnableLocalLifecycleStub", "true"));
        var environment = new StubHostEnvironment { EnvironmentName = Environments.Production };

        var enabled = LocalAiLifecycleStub.IsEnabled(environment, configuration);

        Assert.False(enabled);
    }

    [Fact]
    public void IsEnabled_ReturnsFalseInDevelopmentWhenFlagIsDisabled()
    {
        var configuration = Configuration(("Booking:AiTools:EnableLocalLifecycleStub", "false"));
        var environment = new StubHostEnvironment { EnvironmentName = Environments.Development };

        var enabled = LocalAiLifecycleStub.IsEnabled(environment, configuration);

        Assert.False(enabled);
    }

    [Fact]
    public void IsEnabled_ReturnsTrueOnlyInDevelopmentWhenFlagIsEnabled()
    {
        var configuration = Configuration(("Booking:AiTools:EnableLocalLifecycleStub", "true"));
        var environment = new StubHostEnvironment { EnvironmentName = Environments.Development };

        var enabled = LocalAiLifecycleStub.IsEnabled(environment, configuration);

        Assert.True(enabled);
    }

    [Fact]
    public async Task CurrentUserPermissionClient_ResolvesMockPlatformAdminWhenMockTokensAreEnabled()
    {
        Environment.SetEnvironmentVariable("Booking__AllowMockTokens", "true");
        var client = new CurrentUserPermissionClient(new ThrowingAdviserUserContextClient());

        var result = await client.GetCurrentUserAsync("Bearer mock-token:mock-platform-admin", CancellationToken.None);

        Assert.True(result.IsAuthorised);
        Assert.Equal("mock-platform-admin", result.User?.UserId);
        Assert.Contains("*", result.User!.Permissions);
    }

    [Fact]
    public void CreateResponse_ReturnsExpectedLocalAiLifecyclePayload()
    {
        var user = new AdviserUserContext
        {
            UserId = "mock-platform-admin",
            Email = "platform.admin@afh.co.uk"
        };
        var now = new DateTime(2026, 7, 6, 13, 47, 28, DateTimeKind.Utc);

        var response = LocalAiLifecycleStub.CreateResponse(
            "booking-local-ai-proof",
            user,
            "corr-local-proof",
            now);

        var lifecycleEvent = Assert.Single(response.Events);
        Assert.Equal(LocalAiLifecycleStub.EventId, lifecycleEvent.Id);
        Assert.Equal("booking-local-ai-proof", lifecycleEvent.BookingId);
        Assert.Equal(LocalAiLifecycleStub.EventType, lifecycleEvent.EventType);
        Assert.Equal(LocalAiLifecycleStub.NewState, lifecycleEvent.NewState);
        Assert.Equal("mock-platform-admin", lifecycleEvent.ActorId);
        Assert.Equal("AFH.AI.McpGateway", lifecycleEvent.SourceSystem);
        Assert.Equal("corr-local-proof", lifecycleEvent.CorrelationId);

        var step = Assert.Single(lifecycleEvent.Steps);
        Assert.Equal(LocalAiLifecycleStub.StepId, step.Id);
        Assert.Equal("GatewayRouting", step.StepName);
        Assert.Equal("Succeeded", step.Status);
        Assert.Equal("corr-local-proof", step.CorrelationId);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("Booking__AllowMockTokens", _originalAllowMockTokens);
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value))
            .Build();
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "AFH.Booking.Function";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class ThrowingAdviserUserContextClient : IAdviserUserContextClient
    {
        public Task<AdviserUserContext?> GetCurrentUserAsync(string bearerToken, CancellationToken ct)
        {
            throw new InvalidOperationException("Mock token resolution should not call the downstream adviser context client.");
        }
    }
}
