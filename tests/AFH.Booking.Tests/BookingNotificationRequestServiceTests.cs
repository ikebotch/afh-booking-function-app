using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Abstractions.Notifications;
using AFH.Booking.Application.Models.Lifecycle.Constants;
using AFH.Booking.Application.Models.Notifications;
using AFH.Booking.Application.Services.Notifications;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Client;
using AFH.Notification.Application.Models;
using Moq;
using System.Net;

namespace AFH.Booking.Tests;

public sealed class BookingNotificationRequestServiceTests
{
    [Theory]
    [InlineData("Booked", "BookingConfirmed", "Confirmed")]
    [InlineData("BookingConfirmed", "BookingConfirmed", "Confirmed")]
    [InlineData("Rearranged", "BookingRescheduled", "Rescheduled")]
    [InlineData("BookingRescheduled", "BookingRescheduled", "Rescheduled")]
    [InlineData("Cancelled", "BookingCancelled", "Cancelled")]
    [InlineData("BookingCancelled", "BookingCancelled", "Cancelled")]
    [InlineData("HoldCreated", "BookingHoldCreated", "Held")]
    [InlineData("BookingHoldCreated", "BookingHoldCreated", "Held")]
    public async Task SendAsync_PublishesManualNotificationToOutboxPublisher(
        string eventType,
        string expectedNotificationType,
        string expectedMeetingStatus)
    {
        var notificationStep = new CapturingBookingNotificationStep();
        var sut = CreateSut(notificationStep);

        var result = await sut.SendAsync("booking-1", eventType, null, sendSms: false, sendEmail: true, "corr-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Queued", result.Value?.EmailStatus);
        Assert.Equal("corr-1", result.Value?.DispatchId);
        Assert.NotNull(notificationStep.Request);
        Assert.Equal(expectedNotificationType, notificationStep.BookingNotificationType?.Name);
        Assert.Equal("corr-1", notificationStep.Request!.CorrelationId);
        Assert.Equal(LifecycleActors.System, notificationStep.Request.ActorType);
        var recipient = Assert.Single(notificationStep.Request.Recipients);
        Assert.Equal(BookingNotificationRecipientTypes.Client, recipient.RecipientType);
        Assert.Equal(BookingNotificationChannel.Email, Assert.Single(recipient.PreferredChannels ?? []));
        Assert.Equal("jane.client@example.test", recipient.Email);
        Assert.Equal("Jane Client", notificationStep.Request.Data["clientName"]);
        Assert.Equal("Review", notificationStep.Request.Data["meetingType"]);
        Assert.Equal("Review", notificationStep.Request.Data["meetingTopic"]);
        Assert.Equal("Online", notificationStep.Request.Data["meetingMethod"]);
        Assert.Equal("60 minutes", notificationStep.Request.Data["meetingDuration"]);
        Assert.Equal(expectedMeetingStatus, notificationStep.Request.Data["meetingStatus"]);
        Assert.True(notificationStep.Request.Data.ContainsKey("meetingDateDay"));
        Assert.True(notificationStep.Request.Data.ContainsKey("meetingDateTime"));
        Assert.Equal(notificationStep.Request.Data["meetingDateDay"], notificationStep.Request.Data["date"]);
        Assert.Equal(notificationStep.Request.Data["meetingDateTime"], notificationStep.Request.Data["time"]);
        Assert.Equal(string.Empty, notificationStep.Request.Data["joinMeetingLink"]);
        Assert.Equal(string.Empty, notificationStep.Request.Data["manageBookingLink"]);
    }

    [Theory]
    [InlineData("booking-confirmed")]
    [InlineData("booking-hold")]
    [InlineData("booking-cancelled")]
    [InlineData("booking-rescheduled")]
    public void VariableCatalog_ExposesStandardBookingPayloadVariables(string lifecycleEvent)
    {
        var variables = NotificationTemplateVariableCatalog.ForLifecycleEvent(lifecycleEvent);

        Assert.Contains("clientName", variables);
        Assert.Contains("meetingType", variables);
        Assert.Contains("meetingTopic", variables);
        Assert.Contains("meetingDateDay", variables);
        Assert.Contains("meetingDateTime", variables);
        Assert.Contains("meetingMethod", variables);
        Assert.Contains("meetingDuration", variables);
        Assert.Contains("meetingStatus", variables);
        Assert.Contains("adviserName", variables);
    }

    [Fact]
    public async Task SendAsync_RejectsUnsupportedEventType()
    {
        var notificationStep = new CapturingBookingNotificationStep();
        var sut = CreateSut(notificationStep);

        var result = await sut.SendAsync("booking-1", "BookingChanged", null, sendSms: false, sendEmail: true, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("Unsupported EventType", result.ErrorMessage);
        Assert.Null(notificationStep.Request);
    }

    [Fact]
    public async Task SendAsync_RejectsSmsBecauseQueuedSmsDeliveryIsNotImplemented()
    {
        var notificationStep = new CapturingBookingNotificationStep();
        var sut = CreateSut(notificationStep);

        var result = await sut.SendAsync("booking-1", "Booked", null, sendSms: true, sendEmail: true, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("SMS is not supported", result.ErrorMessage);
        Assert.Null(notificationStep.Request);
    }

    [Fact]
    public async Task SendAsync_RejectsNoSupportedChannel()
    {
        var notificationStep = new CapturingBookingNotificationStep();
        var sut = CreateSut(notificationStep);

        var result = await sut.SendAsync("booking-1", "Booked", null, sendSms: false, sendEmail: false, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("At least one supported notification channel", result.ErrorMessage);
        Assert.Null(notificationStep.Request);
    }

    [Fact]
    public async Task SendAsync_RejectsMessageOverrideUntilQueuedTemplatesSupportIt()
    {
        var notificationStep = new CapturingBookingNotificationStep();
        var sut = CreateSut(notificationStep);

        var result = await sut.SendAsync("booking-1", "Booked", "Custom text", sendSms: false, sendEmail: true, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("MessageOverride is not supported", result.ErrorMessage);
        Assert.Null(notificationStep.Request);
    }

    private static IBookingNotificationRequestService CreateSut(CapturingBookingNotificationStep notificationStep)
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

        return new BookingNotificationRequestService(holds.Object, slots.Object, transactions.Object, clients.Object, notificationStep);
    }

    private sealed class CapturingBookingNotificationStep : IBookingNotificationStep
    {
        public CapturedRequest? Request { get; private set; }
        public BookingNotificationType? BookingNotificationType => Request?.LifecycleEventType switch
        {
            LifecycleEventTypes.Booked => BookingNotificationTypes.BookingConfirmed,
            LifecycleEventTypes.Rearranged => BookingNotificationTypes.BookingRescheduled,
            LifecycleEventTypes.Cancelled => BookingNotificationTypes.BookingCancelled,
            LifecycleEventTypes.HoldCreated => BookingNotificationTypes.BookingHoldCreated,
            _ => null
        };

        public Task<(string Status, string? ErrorCode, string? ErrorDetails)> ExecuteAsync(
            string lifecycleEventType,
            string correlationId,
            string actorType,
            IReadOnlyList<BookingNotificationRecipient> recipients,
            IReadOnlyDictionary<string, string> data,
            CancellationToken ct)
        {
            Request = new CapturedRequest(lifecycleEventType, correlationId, actorType, recipients, data);
            return Task.FromResult<(string, string?, string?)>((LifecycleStepStatuses.Succeeded, null, null));
        }
    }

    private sealed record CapturedRequest(
        string LifecycleEventType,
        string CorrelationId,
        string ActorType,
        IReadOnlyList<BookingNotificationRecipient> Recipients,
        IReadOnlyDictionary<string, string> Data);
}
