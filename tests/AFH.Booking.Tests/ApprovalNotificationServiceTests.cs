using AFH.Booking.Application.Abstractions.Notifications;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Approvals;
using AFH.Booking.Application.Models.AdviserProjection;
using AFH.Booking.Application.Models.Notifications;
using AFH.Notification.Application.Policies.Booking;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AFH.Booking.Tests;

public sealed class ApprovalNotificationServiceTests
{
    [Theory]
    [InlineData("Cancel", "Approved")]
    [InlineData("Cancel", "Rejected")]
    [InlineData("Rearrange", "Approved")]
    [InlineData("Rearrange", "Rejected")]
    public async Task RecordOutcomeAsync_PublishesAdviserOutcomeNotification(
        string changeType,
        string outcome)
    {
        BookingNotificationRequest? published = null;
        var publisher = new Mock<IBookingNotificationPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<BookingNotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<BookingNotificationRequest, CancellationToken>((request, _) => published = request)
            .Returns(Task.CompletedTask);

        var sut = new ApprovalNotificationService(
            NullLogger<ApprovalNotificationService>.Instance,
            publisher.Object,
            AdviserRepository("adviser-1", "adviser@example.com"));

        await sut.RecordOutcomeAsync(
            requestId: "approval-1",
            bookingId: "booking-1",
            transactionId: "tx-1",
            transactionRef: "txn-ref",
            requesterId: "adviser-1",
            approverId: "manager-1",
            outcome: outcome,
            changeType: changeType,
            reasonCode: "CLIENT_REQUEST",
            reasonDetail: "Client asked",
            notes: "Reviewed",
            CancellationToken.None);

        Assert.NotNull(published);
        Assert.Equal("AdviserRequestOutcome", published!.Type.Name);
        var recipient = Assert.Single(published.Recipients);
        Assert.Equal("Adviser", recipient.RecipientType);
        Assert.Equal("adviser@example.com", recipient.Email);
        Assert.Equal("approval-outcome:approval-1:" + outcome, published.Data["IdempotencyKey"]);
        Assert.Equal(changeType, published.Data["changeType"]);
        Assert.Equal(outcome, published.Data["outcome"]);
        Assert.Equal("Reviewed", published.Data["decisionNotes"]);
    }

    [Fact]
    public async Task RecordOutcomeAsync_MissingAdviserEmailSkipsSafely()
    {
        var publisher = new Mock<IBookingNotificationPublisher>();
        var sut = new ApprovalNotificationService(
            NullLogger<ApprovalNotificationService>.Instance,
            publisher.Object,
            AdviserRepository("adviser-1", null));

        await sut.RecordOutcomeAsync(
            "approval-1",
            "booking-1",
            "tx-1",
            "txn-ref",
            "adviser-1",
            "manager-1",
            "Approved",
            "Cancel",
            "CLIENT_REQUEST",
            null,
            null,
            CancellationToken.None);

        publisher.Verify(x => x.PublishAsync(It.IsAny<BookingNotificationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordOutcomeAsync_PublisherFailureDoesNotThrow()
    {
        var publisher = new Mock<IBookingNotificationPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<BookingNotificationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("publisher unavailable"));

        var sut = new ApprovalNotificationService(
            NullLogger<ApprovalNotificationService>.Instance,
            publisher.Object,
            AdviserRepository("adviser-1", "adviser@example.com"));

        await sut.RecordOutcomeAsync(
            "approval-1",
            "booking-1",
            "tx-1",
            "txn-ref",
            "adviser-1",
            "manager-1",
            "Rejected",
            "Rearrange",
            "CLIENT_REQUEST",
            null,
            "No availability",
            CancellationToken.None);

        publisher.Verify(x => x.PublishAsync(It.IsAny<BookingNotificationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void BookingNotificationIdempotencyPolicy_UsesExplicitOutcomeKey()
    {
        var request = new NotificationRequested(
            new NotificationType("Booking", "AdviserRequestOutcome"),
            "corr-1",
            new NotificationActor("Manager", "ApprovalWorkflow", "manager-1", null, null),
            [],
            new Dictionary<string, string>
            {
                ["BookingId"] = "booking-1",
                ["IdempotencyKey"] = "approval-outcome:approval-1:Approved"
            });

        var policy = new BookingNotificationIdempotencyPolicy();

        Assert.True(policy.CanHandle(request));
        Assert.Equal("approval-outcome:approval-1:Approved", policy.GetPrimaryId(request));
    }

    private static IAdviserProfileProjectionRepository AdviserRepository(
        string adviserId,
        string? email)
    {
        var repo = new Mock<IAdviserProfileProjectionRepository>();
        repo.Setup(x => x.GetAsync(adviserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(email is null
                ? new AdviserProfileProjectionRecord
                {
                    AdviserId = adviserId,
                    DisplayName = "Ada Adviser",
                    MailboxUserId = string.Empty
                }
                : new AdviserProfileProjectionRecord
                {
                    AdviserId = adviserId,
                    DisplayName = "Ada Adviser",
                    MailboxUserId = email
                });
        return repo.Object;
    }
}
