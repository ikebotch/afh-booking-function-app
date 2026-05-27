using AFH.Booking.Domain.Options;
using AFH.Booking.Function.Configuration;
using AFH.Notification.Infrastructure.Queue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace AFH.Booking.Tests;

public sealed class ConfigurationValidationTests
{
    [Fact]
    public void InternalApiAuthValidator_RequiresTokenOutsideAnonymousDevelopment()
    {
        var validator = new InternalApiAuthOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(null, new InternalApiAuthOptions());

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void InternalApiAuthValidator_AllowsAnonymousDevelopmentWithoutToken()
    {
        var validator = new InternalApiAuthOptionsValidator(new FakeHostEnvironment(Environments.Development));

        var result = validator.Validate(null, new InternalApiAuthOptions
        {
            AllowAnonymousInDevelopment = true
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void DomainUserAuthValidator_RequiresAudienceAndAuthorityOrTenantWhenEnabled()
    {
        var validator = new DomainUserAuthOptionsValidator();

        var result = validator.Validate(null, new DomainUserAuthOptions
        {
            Enabled = true
        });

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, failure => failure.Contains("Audience", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolveBookingDbConnectionString_PrefersTypedSectionThenConnectionStrings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BookingDb:ConnectionString"] = "Server=typed;",
                ["ConnectionStrings:BookingDb"] = "Server=connectionStrings;"
            })
            .Build();

        var connectionString = typeof(AFH.Booking.Infrastructure.Composition.ServiceCollectionExtensions)
            .GetMethod("ResolveBookingDbConnectionString", BindingFlags.Static | BindingFlags.NonPublic)?
            .Invoke(null, [configuration]) as string;

        Assert.Equal("Server=typed;", connectionString);
    }

    [Fact]
    public void NotificationQueueOptions_BindFromNotificationsQueueSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Queue:QueueName"] = "notifications-send",
                ["Notifications:Queue:ConnectionString"] = "UseDevelopmentStorage=true"
            })
            .Build();

        var options = configuration
            .GetSection(NotificationQueueOptions.SectionName)
            .Get<NotificationQueueOptions>();

        Assert.NotNull(options);
        Assert.Equal("notifications-send", options.QueueName);
        Assert.Equal("UseDevelopmentStorage=true", options.ConnectionString);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "AFH.Booking.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
