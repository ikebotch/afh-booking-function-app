using System.Text.Json;
using AFH.Booking.Function.Functions.V1.Notifications;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Application.Options;
using AFH.Notification.Application.Services;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using AFH.Notification.Infrastructure.Composition;
using AFH.Notification.Infrastructure.Queue;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AFH.Booking.Tests;

public sealed class NotificationOutboxDispatcherTests
{
    private readonly Mock<INotificationOutboxStore> _outboxStore = new();
    private readonly Mock<INotificationService> _notificationService = new();

    [Fact]
    public async Task DispatchDueBatchAsync_ClaimsDueRowsAndMarksSentOnSuccess()
    {
        var item = CreateOutboxItem(NotificationDispatchStatus.Processing, attemptCount: 1);
        _outboxStore.Setup(x => x.ClaimDueBatchAsync(20, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([item]);

        var dispatched = await CreateDispatcher().DispatchDueBatchAsync(CancellationToken.None);

        Assert.Equal(1, dispatched);
        _notificationService.Verify(x => x.PublishAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()), Times.Once);
        _outboxStore.Verify(x => x.MarkSentAsync(item.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchDueBatchAsync_MarksFailedAndSetsNextAttemptOnRetryableFailure()
    {
        var item = CreateOutboxItem(NotificationDispatchStatus.Processing, attemptCount: 2);
        _outboxStore.Setup(x => x.ClaimDueBatchAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([item]);
        _notificationService.Setup(x => x.PublishAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Graph failed"));

        await CreateDispatcher(maxAttempts: 5, retryDelaySeconds: 300).DispatchDueBatchAsync(CancellationToken.None);

        _outboxStore.Verify(x => x.MarkFailedAsync(
            item.Id,
            "Graph failed",
            It.Is<DateTime>(next => next > DateTime.UtcNow.AddSeconds(250)),
            It.IsAny<CancellationToken>()), Times.Once);
        _outboxStore.Verify(x => x.MarkDeadLetteredAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchDueBatchAsync_MarksDeadLetteredAfterMaxAttempts()
    {
        var item = CreateOutboxItem(NotificationDispatchStatus.Processing, attemptCount: 5);
        _outboxStore.Setup(x => x.ClaimDueBatchAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([item]);
        _notificationService.Setup(x => x.PublishAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Graph failed"));

        await CreateDispatcher(maxAttempts: 5).DispatchDueBatchAsync(CancellationToken.None);

        _outboxStore.Verify(x => x.MarkDeadLetteredAsync(item.Id, "Graph failed", It.IsAny<CancellationToken>()), Times.Once);
        _outboxStore.Verify(x => x.MarkFailedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchDueBatchAsync_InvalidPayload_MarksDeadLettered()
    {
        var item = new NotificationOutboxItem(Guid.NewGuid(), "App", "Type", "key", "{ invalid payload }", NotificationDispatchStatus.Processing, null, 1, null, DateTime.UtcNow, DateTime.UtcNow, null);
        _outboxStore.Setup(x => x.ClaimDueBatchAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([item]);

        await CreateDispatcher().DispatchDueBatchAsync(CancellationToken.None);

        _outboxStore.Verify(x => x.MarkDeadLetteredAsync(item.Id, "Invalid payload JSON.", It.IsAny<CancellationToken>()), Times.Once);
        _notificationService.Verify(x => x.PublishAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TimerFunction_AzureQueueMode_NoOps()
    {
        var dispatcher = CreateDispatcher();
        var options = Options.Create(new NotificationOutboxDispatchOptions { DispatcherMode = NotificationOutboxDispatchOptions.AzureQueueMode });
        var function = new DispatchNotificationOutboxFunction(dispatcher, options, NullLogger<DispatchNotificationOutboxFunction>.Instance);
        var context = new Mock<FunctionContext>();
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);

        await function.RunAsync(default!, context.Object);

        _outboxStore.Verify(x => x.ClaimDueBatchAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void AddNotificationInfrastructure_SqlMode_DoesNotRequireQueueSettings()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotificationInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Outbox:DispatcherMode"] = "Sql"
            })
            .Build());
        using var provider = services.BuildServiceProvider();

        Assert.IsType<NoOpNotificationQueuePublisher>(provider.GetRequiredService<INotificationQueuePublisher>());
    }

    [Fact]
    public void AddNotificationInfrastructure_AzureQueueMode_RequiresQueueSettings()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddNotificationInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Outbox:DispatcherMode"] = "AzureQueue",
                ["NotificationQueue:QueueName"] = "notifications-send"
            })
            .Build()));

        Assert.Contains("NotificationQueue:ConnectionString", ex.Message);
    }

    [Fact]
    public void InvalidDispatcherMode_FailsClearly()
    {
        var options = new NotificationOutboxDispatchOptions { DispatcherMode = "Fileshare" };

        var ex = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("DispatcherMode", ex.Message);
        Assert.Contains("Sql", ex.Message);
        Assert.Contains("AzureQueue", ex.Message);
    }

    private NotificationOutboxDispatcher CreateDispatcher(int maxAttempts = 5, int retryDelaySeconds = 300)
        => new(
            _outboxStore.Object,
            _notificationService.Object,
            Options.Create(new NotificationOutboxDispatchOptions
            {
                DispatcherMode = NotificationOutboxDispatchOptions.SqlMode,
                BatchSize = 20,
                MaxAttempts = maxAttempts,
                RetryDelaySeconds = retryDelaySeconds,
                ProcessingLockSeconds = 300
            }),
            NullLogger<NotificationOutboxDispatcher>.Instance);

    private static NotificationOutboxItem CreateOutboxItem(NotificationDispatchStatus status, int attemptCount)
    {
        var request = new NotificationRequested(
            new NotificationType("App", "Type"),
            "corr",
            new NotificationActor("sys", "App", null, null, null),
            Array.Empty<NotificationRecipient>(),
            new Dictionary<string, string>());

        return new NotificationOutboxItem(
            Guid.NewGuid(),
            "App",
            "Type",
            "key",
            JsonSerializer.Serialize(request),
            status,
            null,
            attemptCount,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null);
    }
}
