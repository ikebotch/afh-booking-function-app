using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Infrastructure.Delivery.Email;
using AFH.Notification.Infrastructure.Delivery.Email.Graph;
using AFH.Notification.Infrastructure.Options;
using AFH.Notification.Infrastructure.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Tests;

public sealed class EmailNotificationDeliveryGatewayTests
{
    [Fact]
    public async Task SendAsync_ComposedProvider_ReturnsExplicitNonProductionStatus()
    {
        var gateway = CreateGateway(new EmailDeliveryOptions
        {
            Enabled = true,
            ProviderName = "Composed"
        });

        var result = await gateway.SendAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal("NonProductionComposed", result.Status);
        Assert.NotNull(result.ProviderMessageId);
    }

    [Fact]
    public async Task SendAsync_ConfiguredOff_ReturnsConfiguredOff()
    {
        var gateway = CreateGateway(new EmailDeliveryOptions
        {
            Enabled = false,
            ProviderName = "Graph"
        });

        var result = await gateway.SendAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal("ConfiguredOff", result.Status);
        Assert.Null(result.ProviderMessageId);
    }

    [Fact]
    public async Task SendAsync_UnsupportedProviderConfigured_ThrowsConfigurationError()
    {
        var gateway = CreateGateway(new EmailDeliveryOptions
        {
            Enabled = true,
            ProviderName = "ProductionProvider"
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.SendAsync(CreateRequest(), CancellationToken.None));

        Assert.Contains("no production provider adapter wired", ex.Message);
    }

    [Fact]
    public void Constructor_GraphProviderMissingSenderMailbox_FailsFast()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CreateGraphGateway(
            new GraphEmailOptions
            {
                UseManagedIdentity = true
            },
            new RecordingGraphEmailSender()));

        Assert.Contains("SenderMailbox", ex.Message);
    }

    [Fact]
    public void Constructor_GraphProviderMissingTenantIdAndNoManagedIdentity_FailsFast()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CreateGraphGateway(
            new GraphEmailOptions
            {
                UseManagedIdentity = false,
                SenderMailbox = "sender@example.test"
            },
            new RecordingGraphEmailSender()));

        Assert.Contains("TenantId", ex.Message);
    }

    [Fact]
    public void Constructor_GraphProviderMissingClientSecretAndNoManagedIdentity_FailsFast()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CreateGraphGateway(
            new GraphEmailOptions
            {
                UseManagedIdentity = false,
                SenderMailbox = "sender@example.test",
                TenantId = "tenant-id",
                ClientId = "client-id"
            },
            new RecordingGraphEmailSender()));

        Assert.Contains("ClientSecret", ex.Message);
    }

    [Fact]
    public async Task SendAsync_GraphProvider_InvokesGraphAdapter()
    {
        var sender = new RecordingGraphEmailSender();
        var gateway = CreateGraphGateway(
            new GraphEmailOptions
            {
                UseManagedIdentity = true,
                SenderMailbox = "sender@example.test"
            },
            sender);

        var request = CreateRequest();
        var result = await gateway.SendAsync(request, CancellationToken.None);

        Assert.Equal("GraphAccepted", result.Status);
        Assert.StartsWith("graph-sendmail-", result.ProviderMessageId);
        Assert.Same(request, sender.LastRequest);
        Assert.Equal("sender@example.test", sender.LastSenderMailbox);
    }

    [Fact]
    public async Task SendAsync_GraphSendFailure_BubblesForQueueRetry()
    {
        var expected = new InvalidOperationException("Graph rejected the request.");
        var gateway = CreateGraphGateway(
            new GraphEmailOptions
            {
                UseManagedIdentity = true,
                SenderMailbox = "sender@example.test"
            },
            new RecordingGraphEmailSender(expected));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gateway.SendAsync(CreateRequest(), CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void AddNotificationInfrastructure_GraphProvider_RegistersGraphGateway()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Email:Enabled"] = "true",
                ["Notifications:Email:ProviderName"] = "Graph",
                ["Notifications:Email:Graph:UseManagedIdentity"] = "true",
                ["Notifications:Email:Graph:SenderMailbox"] = "sender@example.test",
                ["Notifications:Queue:QueueName"] = "notifications-send",
                ["Notifications:Queue:ConnectionString"] = "UseDevelopmentStorage=true",
                ["ConnectionStrings:NotificationDb"] = "Server=localhost;Database=NotificationDb;Trusted_Connection=True;"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddNotificationInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Contains(provider.GetServices<AFH.Notification.Application.Abstractions.INotificationDeliveryGateway>(), gateway => gateway is GraphEmailDeliveryGateway);
    }

    [Fact]
    public void LocalSettingsTemplate_DoesNotContainRealGraphSecrets()
    {
        var template = File.ReadAllText(FindFile("src/AFH.Booking.Function/local.settings.template.json"));

        Assert.Contains("\"Notifications:Email:Graph:ClientSecret\": \"<set-in-key-vault-or-app-settings>\"", template);
        Assert.DoesNotContain("client_secret", template, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", template, StringComparison.OrdinalIgnoreCase);
    }

    private static EmailNotificationDeliveryGateway CreateGateway(EmailDeliveryOptions options)
        => new(Options.Create(options), NullLogger<EmailNotificationDeliveryGateway>.Instance);

    private static GraphEmailDeliveryGateway CreateGraphGateway(GraphEmailOptions options, IGraphEmailSender sender)
        => new(Options.Create(options), sender, NullLogger<GraphEmailDeliveryGateway>.Instance);

    private static NotificationDeliveryRequest CreateRequest()
        => new(
            "corr-1",
            NotificationChannel.Email,
            new NotificationRecipient("Client", "Jane Client", "jane@example.test"),
            "Subject",
            HtmlBody: null,
            "Body",
            new Dictionary<string, string>());

    private static string FindFile(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException($"Could not find {relativePath} from {AppContext.BaseDirectory}.");
    }

    private sealed class RecordingGraphEmailSender : IGraphEmailSender
    {
        private readonly Exception? _exception;

        public RecordingGraphEmailSender(Exception? exception = null)
        {
            _exception = exception;
        }

        public string? LastSenderMailbox { get; private set; }
        public NotificationDeliveryRequest? LastRequest { get; private set; }

        public Task SendAsync(
            string senderMailbox,
            NotificationDeliveryRequest request,
            string providerCorrelationId,
            CancellationToken ct)
        {
            LastSenderMailbox = senderMailbox;
            LastRequest = request;

            if (_exception != null)
                throw _exception;

            return Task.CompletedTask;
        }
    }
}
