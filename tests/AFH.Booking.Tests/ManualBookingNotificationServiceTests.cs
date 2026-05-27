using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Notifications;
using AFH.Booking.Application.Services.Notifications;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Client;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using Moq;
using System.Net;

namespace AFH.Booking.Tests;

public sealed class ManualBookingNotificationServiceTests
{
    [Theory]
    [InlineData("Booked", "BookingConfirmed")]
    [InlineData("BookingConfirmed", "BookingConfirmed")]
    [InlineData("Rearranged", "BookingRescheduled")]
    [InlineData("BookingRescheduled", "BookingRescheduled")]
    [InlineData("Cancelled", "BookingCancelled")]
    [InlineData("BookingCancelled", "BookingCancelled")]
    [InlineData("HoldCreated", "BookingHoldCreated")]
    [InlineData("BookingHoldCreated", "BookingHoldCreated")]
    public async Task SendAsync_PublishesManualNotificationToOutboxPublisher(string eventType, string expectedNotificationType)
    {
        var publisher = new CapturingNotificationPublisher();
        var sut = CreateSut(publisher);

        var result = await sut.SendAsync("booking-1", eventType, null, sendSms: false, sendEmail: true, "corr-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Queued", result.Value?.EmailStatus);
        Assert.Equal("corr-1", result.Value?.DispatchId);
        Assert.NotNull(publisher.LastNotification);
        Assert.Equal("Booking", publisher.LastNotification!.SourceSystem);
        Assert.Equal(expectedNotificationType, publisher.LastNotification.Type.Name);
        Assert.Equal("corr-1", publisher.LastNotification.CorrelationId);
        Assert.Equal("Internal", publisher.LastNotification.Actor.ActorType);
        var recipient = Assert.Single(publisher.LastNotification.Recipients);
        Assert.Equal(BookingNotificationRecipientTypes.Client, recipient.RecipientType);
        Assert.Equal(NotificationChannel.Email, Assert.Single(recipient.PreferredChannels ?? []));
        Assert.Equal("jane.client@example.test", recipient.Email);
    }

    [Fact]
    public async Task SendAsync_RejectsUnsupportedEventType()
    {
        var publisher = new CapturingNotificationPublisher();
        var sut = CreateSut(publisher);

        var result = await sut.SendAsync("booking-1", "BookingChanged", null, sendSms: false, sendEmail: true, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("Unsupported EventType", result.ErrorMessage);
        Assert.Null(publisher.LastNotification);
    }

    [Fact]
    public async Task SendAsync_RejectsSmsBecauseQueuedSmsDeliveryIsNotImplemented()
    {
        var publisher = new CapturingNotificationPublisher();
        var sut = CreateSut(publisher);

        var result = await sut.SendAsync("booking-1", "Booked", null, sendSms: true, sendEmail: true, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("SMS is not supported", result.ErrorMessage);
        Assert.Null(publisher.LastNotification);
    }

    [Fact]
    public async Task SendAsync_RejectsNoSupportedChannel()
    {
        var publisher = new CapturingNotificationPublisher();
        var sut = CreateSut(publisher);

        var result = await sut.SendAsync("booking-1", "Booked", null, sendSms: false, sendEmail: false, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("At least one supported notification channel", result.ErrorMessage);
        Assert.Null(publisher.LastNotification);
    }

    [Fact]
    public async Task SendAsync_RejectsMessageOverrideUntilQueuedTemplatesSupportIt()
    {
        var publisher = new CapturingNotificationPublisher();
        var sut = CreateSut(publisher);

        var result = await sut.SendAsync("booking-1", "Booked", "Custom text", sendSms: false, sendEmail: true, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("MessageOverride is not supported", result.ErrorMessage);
        Assert.Null(publisher.LastNotification);
    }

    private static IManualBookingNotificationService CreateSut(CapturingNotificationPublisher publisher)
    {
        var now = DateTime.UtcNow;
        var hold = BookingHold.Rehydrate(
            "booking-1",
            "slot-1",
            "user-1",
            BookingHoldStatus.Confirmed,
            now.AddHours(-1),
            now.AddHours(1),
            now.AddMinutes(-10),
            null,
            null,
            null,
            "event-1",
            null);

        var slot = BookingSlot.Rehydrate(
            "slot-1",
            "tx-1",
            "adv-1",
            "Adviser One",
            now.AddDays(1),
            now.AddDays(1).AddHours(1),
            10,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            now.AddHours(-2));

        var tx = BookingTransaction.Rehydrate(
            "tx-1",
            "TRX-1",
            now.AddDays(1),
            TimeSpan.FromHours(1),
            "UTC",
            true,
            "Review",
            null,
            BookingTransactionStatus.Completed,
            now.AddHours(-3),
            now.AddDays(2));

        var holds = new Mock<IBookingHoldRepository>();
        holds.Setup(x => x.GetAsync("booking-1", It.IsAny<CancellationToken>())).ReturnsAsync(hold);

        var slots = new Mock<IBookingSlotRepository>();
        slots.Setup(x => x.GetAsync("slot-1", It.IsAny<CancellationToken>())).ReturnsAsync(slot);

        var transactions = new Mock<IBookingTransactionRepository>();
        transactions.Setup(x => x.GetAsync("tx-1", It.IsAny<CancellationToken>())).ReturnsAsync(tx);

        var clients = new Mock<IClientDirectory>();
        clients.Setup(x => x.GetAsync("TRX-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientDirectoryItem
            {
                FirstName = "Jane",
                LastName = "Client",
                Email = "jane.client@example.test",
                Phone = "+447700900123"
            });

        return new ManualBookingNotificationService(holds.Object, slots.Object, transactions.Object, clients.Object, publisher);
    }

    private sealed class CapturingNotificationPublisher : INotificationPublisher
    {
        public NotificationRequested? LastNotification { get; private set; }

        public Task PublishAsync(NotificationRequested notification, CancellationToken ct)
        {
            LastNotification = notification;
            return Task.CompletedTask;
        }
    }
}
