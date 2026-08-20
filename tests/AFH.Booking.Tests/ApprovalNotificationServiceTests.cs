using AFH.Booking.Application.Abstractions.Notifications;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Approvals;
using AFH.Booking.Application.Models.AdviserProjection;
using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Application.Models.Notifications;
using AFH.Booking.Domain.Bookings;
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
    public async Task RecordOutcomeAsync_PublishesPolicyResolvedOutcomeNotification(
        string changeType,
        string outcome)
    {
        BookingNotificationRequest? published = null;
        var publisher = new Mock<IBookingNotificationPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<BookingNotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<BookingNotificationRequest, CancellationToken>((request, _) => published = request)
            .Returns(Task.CompletedTask);

        var sut = CreateService(publisher.Object, adviserEmail: "adviser@example.com");

        await sut.RecordOutcomeAsync(
            Approval(changeType, outcome),
            ApprovalBooking(),
            approverId: "manager-1",
            CancellationToken.None);

        Assert.NotNull(published);
        Assert.Equal("AdviserRequestOutcome", published!.Type.Name);
        Assert.Equal(
            [BookingNotificationRecipientTypes.Adviser, BookingNotificationRecipientTypes.Client, BookingNotificationRecipientTypes.Manager],
            published.Recipients.Select(x => x.RecipientType).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        Assert.Equal("approval-outcome:approval-1:" + outcome, published.Data["IdempotencyKey"]);
        Assert.Equal(changeType, published.Data["changeType"]);
        Assert.Equal(outcome, published.Data["outcome"]);
        Assert.Equal("Reviewed", published.Data["decisionNotes"]);
        Assert.Equal("adviser-request-outcome", published.Data["TemplateKey:Email"]);
    }

    [Fact]
    public async Task RecordRequestSubmittedAsync_PublishesPolicyResolvedSubmittedNotification()
    {
        BookingNotificationRequest? published = null;
        var publisher = new Mock<IBookingNotificationPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<BookingNotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<BookingNotificationRequest, CancellationToken>((request, _) => published = request)
            .Returns(Task.CompletedTask);

        var sut = CreateService(publisher.Object, adviserEmail: "adviser@example.com");

        await sut.RecordRequestSubmittedAsync(
            new ApprovalRouteTarget("Role", "booking-approvers", "Booking Approvers"),
            Approval("Cancel", "Pending"),
            ApprovalBooking(),
            "adviser-1",
            CancellationToken.None);

        Assert.NotNull(published);
        Assert.Equal("AdviserRequestSubmitted", published!.Type.Name);
        Assert.Equal("approval-submitted:approval-1", published.Data["IdempotencyKey"]);
        Assert.Equal("Submitted", published.Data["outcome"]);
        Assert.Equal("Pending", published.Data["status"]);
        Assert.Equal("adviser-request-submitted", published.Data["TemplateKey:Email"]);
        Assert.Equal(
            [BookingNotificationRecipientTypes.Adviser, BookingNotificationRecipientTypes.Client, BookingNotificationRecipientTypes.Manager],
            published.Recipients.Select(x => x.RecipientType).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task RecordOutcomeAsync_MissingAdviserEmailStillPublishesOtherResolvedRecipients()
    {
        BookingNotificationRequest? published = null;
        var publisher = new Mock<IBookingNotificationPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<BookingNotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<BookingNotificationRequest, CancellationToken>((request, _) => published = request)
            .Returns(Task.CompletedTask);
        var sut = CreateService(publisher.Object, adviserEmail: null);

        await sut.RecordOutcomeAsync(
            Approval("Cancel", "Approved"),
            ApprovalBooking(),
            "manager-1",
            CancellationToken.None);

        Assert.NotNull(published);
        Assert.Equal(
            [BookingNotificationRecipientTypes.Client, BookingNotificationRecipientTypes.Manager],
            published!.Recipients.Select(x => x.RecipientType).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task RecordOutcomeAsync_PublisherFailureDoesNotThrow()
    {
        var publisher = new Mock<IBookingNotificationPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<BookingNotificationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("publisher unavailable"));

        var sut = CreateService(publisher.Object, adviserEmail: "adviser@example.com");

        await sut.RecordOutcomeAsync(
            Approval("Rearrange", "Rejected"),
            ApprovalBooking(),
            "manager-1",
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

    private static ApprovalNotificationService CreateService(
        IBookingNotificationPublisher publisher,
        string? adviserEmail)
        => new(
            NullLogger<ApprovalNotificationService>.Instance,
            publisher,
            AdviserRepository("adviser-1", adviserEmail),
            new StubPolicyProvider(),
            new StubRecipientResolver());

    private static ApprovalWorkflowRecord Approval(string changeType, string status)
        => new()
        {
            Id = "approval-1",
            Reference = "REQ-1",
            BookingId = "booking-1",
            TransactionId = "tx-1",
            ChangeType = changeType,
            RequestedBy = "Adviser",
            RequesterId = "adviser-1",
            Status = status,
            ReasonCode = "CLIENT_REQUEST",
            ReasonDetail = "Client asked",
            ReviewNotes = "Reviewed",
            ClientName = "Alice Client",
            AdviserName = "Ada Adviser",
            MeetingType = "Review",
            BookingDateTime = FixedNow.AddDays(1)
        };

    private static ApprovalBookingSnapshot ApprovalBooking()
    {
        var slot = BookingSlot.Rehydrate(
            "slot-1",
            "tx-1",
            "adviser-1",
            "Ada Adviser",
            FixedNow.AddDays(1),
            FixedNow.AddDays(1).AddHours(1),
            5,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            FixedNow);
        var hold = BookingHold.Rehydrate(
            "booking-1",
            "slot-1",
            "user-1",
            BookingHoldStatus.Confirmed,
            FixedNow.AddHours(-2),
            FixedNow.AddHours(1),
            FixedNow.AddHours(-1),
            null,
            null,
            null,
            null,
            null,
            "BK-1");
        var transaction = BookingTransaction.Rehydrate(
            "tx-1",
            "txn-ref",
            "BK-1",
            "Alice Client",
            "client@example.com",
            "1 Client Street",
            null,
            "Client Town",
            null,
            "AB1 2CD",
            FixedNow,
            TimeSpan.FromHours(1),
            "Europe/London",
            true,
            "Review",
            null,
            BookingTransactionStatus.Open,
            FixedNow,
            null,
            [slot]);

        return new ApprovalBookingSnapshot(hold, slot, transaction);
    }

    private static readonly DateTime FixedNow = new(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc);

    private sealed class StubPolicyProvider : IBookingNotificationPolicyProvider
    {
        public Task<BookingNotificationPolicy> GetAsync(
            string sourceApplication,
            BookingNotificationType notificationType,
            CancellationToken ct)
            => Task.FromResult(new BookingNotificationPolicy(
                sourceApplication,
                notificationType.Name,
                true,
                [new BookingNotificationChannelPolicy(BookingNotificationChannel.Email, true, notificationType.Name == BookingNotificationTypes.AdviserRequestSubmittedName ? "adviser-request-submitted" : "adviser-request-outcome", "v1")],
                [
                    new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.Client, true),
                    new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.Adviser, true),
                    new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.Manager, true)
                ]));
    }

    private sealed class StubRecipientResolver : IBookingNotificationRecipientResolver
    {
        public Task<IReadOnlyList<BookingNotificationRecipient>> ResolveAsync(
            BookingNotificationPolicy policy,
            IReadOnlyList<BookingNotificationRecipient> requestedRecipients,
            IReadOnlyDictionary<string, string> data,
            CancellationToken ct)
        {
            var recipients = requestedRecipients.ToList();
            if (policy.Recipients.Any(x => x.Enabled && x.RecipientType == BookingNotificationRecipientTypes.Manager))
            {
                recipients.Add(new BookingNotificationRecipient(
                    BookingNotificationRecipientTypes.Manager,
                    "Booking Manager",
                    "manager@example.com",
                    PreferredChannels: [BookingNotificationChannel.Email]));
            }

            return Task.FromResult<IReadOnlyList<BookingNotificationRecipient>>(recipients);
        }
    }
}
