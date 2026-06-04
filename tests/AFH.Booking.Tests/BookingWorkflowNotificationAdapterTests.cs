using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Models.Lifecycle;
using AFH.Booking.Application.Models.Lifecycle.Constants;
using AFH.Booking.Application.Models.Notifications;
using AFH.Booking.Application.Services.Lifecycle;
using Moq;

namespace AFH.Booking.Tests;

public sealed class BookingWorkflowNotificationAdapterTests
{
    [Fact]
    public async Task RequestAsync_WhenStepSucceeds_ReturnsSucceededOutcome()
    {
        var step = CreateStep((LifecycleStepStatuses.Succeeded, null, null));
        var sut = new BookingWorkflowNotificationAdapter(step.Object);

        var outcome = await sut.RequestAsync(Request(LifecycleEventTypes.Booked), CancellationToken.None);

        Assert.Equal("BookingConfirmed", outcome.NotificationType);
        Assert.Equal(BookingWorkflowNotificationOutcomeStatuses.Succeeded, outcome.Status);
        Assert.Equal(LifecycleStepStatuses.Succeeded, outcome.ToLifecycleStepStatus());
        Assert.Null(outcome.ToLifecycleStepErrorCode());
        step.Verify(x => x.ExecuteAsync(
            LifecycleEventTypes.Booked,
            "booking-1",
            LifecycleActors.Client,
            It.IsAny<IReadOnlyList<BookingNotificationRecipient>>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(BookingWorkflowNotificationOutcomeStatuses.SkippedPolicyDisabled)]
    [InlineData(BookingWorkflowNotificationOutcomeStatuses.SkippedNoRecipients)]
    [InlineData(BookingWorkflowNotificationOutcomeStatuses.SkippedNoChannels)]
    public async Task RequestAsync_WhenStepSkips_ReturnsStructuredSkippedOutcome(string skipCode)
    {
        var step = CreateStep((LifecycleStepStatuses.Skipped, skipCode, "safe skip reason"));
        var sut = new BookingWorkflowNotificationAdapter(step.Object);

        var outcome = await sut.RequestAsync(Request(LifecycleEventTypes.Cancelled), CancellationToken.None);

        Assert.Equal("BookingCancelled", outcome.NotificationType);
        Assert.Equal(skipCode, outcome.Status);
        Assert.Equal(LifecycleStepStatuses.Skipped, outcome.ToLifecycleStepStatus());
        Assert.Equal(skipCode, outcome.ToLifecycleStepErrorCode());
        Assert.Contains(skipCode, outcome.ToLifecycleStepDetails());
    }

    [Fact]
    public async Task RequestAsync_WhenStepFails_ReturnsSafeFailedOutcome()
    {
        var step = CreateStep((LifecycleStepStatuses.Failed, LifecycleErrorCodes.NotificationFailed, "smtp token=https://client.example/?token=secret body=hello"));
        var sut = new BookingWorkflowNotificationAdapter(step.Object);

        var outcome = await sut.RequestAsync(Request(LifecycleEventTypes.Rearranged), CancellationToken.None);
        var details = outcome.ToLifecycleStepDetails();

        Assert.Equal("BookingRescheduled", outcome.NotificationType);
        Assert.Equal(BookingWorkflowNotificationOutcomeStatuses.Failed, outcome.Status);
        Assert.Equal(LifecycleStepStatuses.Failed, outcome.ToLifecycleStepStatus());
        Assert.Equal(LifecycleErrorCodes.NotificationFailed, outcome.ToLifecycleStepErrorCode());
        Assert.DoesNotContain("secret", details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("body=hello", details, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Notification handoff failed.", details);
    }

    [Fact]
    public void ToLifecycleStepDetails_DoesNotIncludeRecipientsOrNotificationData()
    {
        var request = Request(
            LifecycleEventTypes.HoldCreated,
            new Dictionary<string, string>
            {
                ["viewBookingUrl"] = "https://client.example/bookings/booking-1?token=super-secret",
                ["emailBody"] = "full email body",
                ["smsBody"] = "full sms body"
            });
        var outcome = BookingWorkflowNotificationOutcome.Succeeded("BookingHoldCreated", request.Recipients.Count);

        var details = outcome.ToLifecycleStepDetails();

        Assert.Contains("BookingHoldCreated", details);
        Assert.DoesNotContain("super-secret", details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("full email body", details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("full sms body", details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("client@example.test", details, StringComparison.OrdinalIgnoreCase);
    }

    private static Mock<IBookingNotificationStep> CreateStep(
        (string Status, string? ErrorCode, string? ErrorDetails) result)
    {
        var step = new Mock<IBookingNotificationStep>();
        step.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<BookingNotificationRecipient>>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return step;
    }

    private static BookingWorkflowNotificationRequest Request(
        string lifecycleEventType,
        IReadOnlyDictionary<string, string>? data = null) =>
        new(
            lifecycleEventType,
            "booking-1",
            LifecycleActors.Client,
            [new BookingNotificationRecipient(BookingNotificationRecipientTypes.Client, "Jane Client", "client@example.test")],
            data ?? new Dictionary<string, string> { ["bookingId"] = "booking-1" });
}
