using System.Text.Json;
using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Approvals;
using AFH.Booking.Application.Bookings;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.Models.AdviserProjection;
using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Application.Services.AdviserProjection;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using Moq;

namespace AFH.Booking.Tests;

public sealed class CancellationWorkflowContextTests
{
    private static readonly DateTime FixedNow = new(2026, 06, 04, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CancelAsync_SelfServiceActorContext_RecordsClientActorAndSelfServiceSource()
    {
        var actor = BookingActorContext.SelfServiceClient("client-1", "corr-self");
        var harness = CancellationHarness.Create();

        var result = await harness.Sut.CancelAsync(new CancelBookingCommand
        {
            BookingId = "booking-1",
            ActorContext = actor,
            ReasonCode = "CLIENT_REQUEST"
        }, sendClientNotification: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var recorded = Assert.Single(harness.Events);
        Assert.Same(actor, recorded.ActorContext);
        Assert.True(recorded.ActorContext?.IsSelfService);
        Assert.Equal(LifecycleActors.Client, recorded.ActorType);
        Assert.Equal("client-1", recorded.ActorId);
        Assert.Equal("corr-self", recorded.CorrelationId);
        Assert.Equal(BookingActorContext.SourceSelfService, recorded.SourceSystem);
    }

    [Fact]
    public async Task CancelAsync_LeadTechActorContext_RecordsLeadTechActorAndSource()
    {
        var actor = BookingActorContext.LeadTech(
            actorId: "leadtech-user",
            displayName: "LeadTech User",
            correlationId: "corr-leadtech");
        var harness = CancellationHarness.Create();

        var result = await harness.Sut.CancelAsync(new CancelBookingCommand
        {
            BookingId = "booking-1",
            ActorContext = actor,
            ReasonCode = "LEADTECH_REQUEST"
        }, sendClientNotification: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var recorded = Assert.Single(harness.Events);
        Assert.Same(actor, recorded.ActorContext);
        Assert.Equal(LifecycleActors.LeadTech, recorded.ActorType);
        Assert.Equal("leadtech-user", recorded.ActorId);
        Assert.Equal("corr-leadtech", recorded.CorrelationId);
        Assert.Equal(BookingActorContext.SourceLeadTech, recorded.SourceSystem);
    }

    [Fact]
    public async Task CancelAsync_InternalAdminActorContext_DoesNotRecordClientByDefault()
    {
        var actor = BookingActorContext.InternalAdmin(
            actorId: "admin-1",
            displayName: "Admin One",
            correlationId: "corr-admin");
        var harness = CancellationHarness.Create();

        var result = await harness.Sut.CancelAsync(new CancelBookingCommand
        {
            BookingId = "booking-1",
            ActorContext = actor,
            ReasonCode = "ADMIN_REQUEST"
        }, sendClientNotification: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var recorded = Assert.Single(harness.Events);
        Assert.Equal(BookingActorContext.ActorInternalAdmin, recorded.ActorType);
        Assert.NotEqual(LifecycleActors.Client, recorded.ActorType);
        Assert.Equal("admin-1", recorded.ActorId);
        Assert.Equal(BookingActorContext.SourceInternalAdmin, recorded.SourceSystem);
    }

    [Fact]
    public async Task CancelAsync_LegacyCommandWithoutActorContext_UsesLegacyFields()
    {
        var harness = CancellationHarness.Create();

        var result = await harness.Sut.CancelAsync(new CancelBookingCommand
        {
            BookingId = "booking-1",
            RequestedBy = LifecycleActors.Client,
            ActorId = "legacy-client",
            CorrelationId = "legacy-corr",
            ReasonCode = "CLIENT_REQUEST"
        }, sendClientNotification: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var recorded = Assert.Single(harness.Events);
        Assert.Null(recorded.ActorContext);
        Assert.Equal(LifecycleActors.Client, recorded.ActorType);
        Assert.Equal("legacy-client", recorded.ActorId);
        Assert.Equal("legacy-corr", recorded.CorrelationId);
        Assert.Equal("BookingService", recorded.SourceSystem);
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelledNonClient_ReturnsIdempotentSuccessWithoutLifecycle()
    {
        var harness = CancellationHarness.Create(BookingHoldStatus.Cancelled);

        var result = await harness.Sut.CancelAsync(new CancelBookingCommand
        {
            BookingId = "booking-1",
            ActorContext = BookingActorContext.InternalAdmin("admin-1")
        }, sendClientNotification: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Empty(harness.Events);
        harness.Calendar.Verify(x => x.CancelBookingEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.Downstream.Verify(x => x.PublishBookingChangeAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelAsync_CalendarFailure_RecordsFailedOutlookStepAndStillCancels()
    {
        var harness = CancellationHarness.Create(providerEventId: "provider-1");
        harness.Calendar.Setup(x => x.CancelBookingEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("calendar unavailable"));

        var result = await harness.Sut.CancelAsync(new CancelBookingCommand
        {
            BookingId = "booking-1",
            ActorContext = BookingActorContext.InternalAdmin("admin-1"),
            ReasonCode = "ADMIN_REQUEST"
        }, sendClientNotification: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var outlook = Assert.Single(harness.Steps, x => x.StepName == LifecycleStepNames.Outlook);
        Assert.Equal(LifecycleStepStatuses.Failed, outlook.Status);
        Assert.Equal(LifecycleErrorCodes.CalendarCancelFailed, outlook.ErrorCode);
        harness.Downstream.Verify(x => x.PublishBookingChangeAsync(
            "booking-1",
            "Cancel",
            "txn-ref",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_NotificationBehaviour_RemainsControlledBySendClientNotificationFlag()
    {
        var harness = CancellationHarness.Create();

        var result = await harness.Sut.CancelAsync(new CancelBookingCommand
        {
            BookingId = "booking-1",
            ActorContext = BookingActorContext.InternalAdmin("admin-1"),
            ReasonCode = "ADMIN_REQUEST"
        }, sendClientNotification: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        harness.NotificationStep.Verify(x => x.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<BookingNotificationRecipient>>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        var notification = Assert.Single(harness.Steps, x => x.StepName == LifecycleStepNames.Notifications);
        Assert.Equal(LifecycleStepStatuses.Skipped, notification.Status);
    }

    [Fact]
    public async Task ApprovalReview_CancelExecution_PreservesApprovalActorSourceAndRequestId()
    {
        CancelBookingCommand? capturedCancel = null;
        var row = new ApprovalWorkflowRecord
        {
            Id = "approval-1",
            BookingId = "booking-1",
            TransactionId = "tx-1",
            ChangeType = "Cancel",
            RequestedBy = LifecycleActors.Adviser,
            RequesterId = "adviser-1",
            Status = "Pending",
            RequestedUtc = FixedNow.AddMinutes(-5),
            ReasonCode = "ADVISER_REQUEST",
            ReasonDetail = "Needs manager approval",
            RequestedPayloadJson = "{}",
            ApproverTargetDisplayName = "Manager"
        };
        var store = new Mock<IApprovalWorkflowStore>();
        store.Setup(x => x.GetForUpdateAsync("approval-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);
        store.Setup(x => x.LoadBookingAsync("booking-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApprovalBooking());
        var cancellation = new Mock<ICancellationOrchestrator>();
        cancellation.Setup(x => x.CancelAsync(It.IsAny<CancelBookingCommand>(), true, It.IsAny<CancellationToken>()))
            .Callback<CancelBookingCommand, bool, CancellationToken>((cmd, _, _) => capturedCancel = cmd)
            .ReturnsAsync(Result<CancelBookingResponse>.Ok(new CancelBookingResponse
            {
                BookingId = "booking-1",
                CancelledUtc = FixedNow,
                Status = "Cancelled"
            }));
        var audit = new Mock<ILifecycleAuditService>();
        audit.Setup(x => x.RecordEventAsync(It.IsAny<LifecycleAuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("approval-event-1");
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var sut = new ApprovalWorkflowService(
            store.Object,
            Mock.Of<IApprovalRoutingService>(),
            cancellation.Object,
            Mock.Of<IRearrangementOrchestrator>(),
            audit.Object,
            Mock.Of<IApprovalNotificationService>(),
            uow.Object,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var response = await sut.ReviewAsync(new ReviewApprovalWorkflowRequest(
            "approval-1",
            Approved: true,
            Reviewer: "manager-1",
            Notes: "Approved",
            CorrelationId: "corr-approval"), CancellationToken.None);

        Assert.NotNull(response);
        Assert.NotNull(capturedCancel);
        Assert.Equal("approval-1", capturedCancel!.ApprovalRequestId);
        Assert.Equal(BookingActorContext.SourceApprovalWorkflow, capturedCancel.ActorContext?.SourceApplication);
        Assert.Equal(LifecycleActors.Adviser, capturedCancel.ActorContext?.ActorType);
        Assert.Equal("adviser-1", capturedCancel.ActorContext?.ActorId);
        Assert.Equal("corr-approval", capturedCancel.ActorContext?.CorrelationId);
    }

    private static ApprovalBookingSnapshot ApprovalBooking()
    {
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
            null);
        var slot = BookingSlot.Rehydrate(
            "slot-1",
            "tx-1",
            "adviser-1",
            "Adviser One",
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
        var tx = BookingTransaction.Rehydrate(
            "tx-1",
            "txn-ref",
            FixedNow,
            TimeSpan.FromHours(1),
            "Europe/London",
            false,
            "Review",
            null,
            BookingTransactionStatus.Open,
            FixedNow,
            null);

        return new ApprovalBookingSnapshot(hold, slot, tx);
    }

    private sealed class CancellationHarness
    {
        private CancellationHarness(
            CancellationOrchestrator sut,
            Mock<ICalendarGateway> calendar,
            Mock<IDownstreamUpdateService> downstream,
            Mock<IBookingNotificationStep> notificationStep,
            List<BookingLifecycleEventRecord> events,
            List<BookingLifecycleStepRecord> steps)
        {
            Sut = sut;
            Calendar = calendar;
            Downstream = downstream;
            NotificationStep = notificationStep;
            Events = events;
            Steps = steps;
        }

        public CancellationOrchestrator Sut { get; }
        public Mock<ICalendarGateway> Calendar { get; }
        public Mock<IDownstreamUpdateService> Downstream { get; }
        public Mock<IBookingNotificationStep> NotificationStep { get; }
        public List<BookingLifecycleEventRecord> Events { get; }
        public List<BookingLifecycleStepRecord> Steps { get; }

        public static CancellationHarness Create(
            BookingHoldStatus holdStatus = BookingHoldStatus.Confirmed,
            string? providerEventId = null)
        {
            var events = new List<BookingLifecycleEventRecord>();
            var steps = new List<BookingLifecycleStepRecord>();
            var hold = BookingHold.Rehydrate(
                "booking-1",
                "slot-1",
                "user-1",
                holdStatus,
                FixedNow.AddHours(-2),
                FixedNow.AddHours(1),
                holdStatus == BookingHoldStatus.Confirmed ? FixedNow.AddHours(-1) : null,
                null,
                holdStatus == BookingHoldStatus.Cancelled ? FixedNow.AddMinutes(-10) : null,
                holdStatus == BookingHoldStatus.Cancelled ? "Already cancelled" : null,
                providerEventId,
                null);
            var slot = BookingSlot.Rehydrate(
                "slot-1",
                "tx-1",
                "adviser-1",
                "Adviser One",
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
            var tx = BookingTransaction.Rehydrate(
                "tx-1",
                "txn-ref",
                FixedNow,
                TimeSpan.FromHours(1),
                "Europe/London",
                false,
                "Review",
                null,
                BookingTransactionStatus.Open,
                FixedNow,
                null);

            var holds = new Mock<IBookingHoldRepository>();
            holds.Setup(x => x.GetAsync("booking-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(hold);
            holds.Setup(x => x.UpdateAsync(It.IsAny<BookingHold>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var slots = new Mock<IBookingSlotRepository>();
            slots.Setup(x => x.GetAsync("slot-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(slot);
            var transactions = new Mock<IBookingTransactionRepository>();
            transactions.Setup(x => x.GetAsync("tx-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(tx);
            var uow = new Mock<IUnitOfWork>();
            uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            var calendar = new Mock<ICalendarGateway>();
            calendar.Setup(x => x.CancelBookingEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var notificationStep = new Mock<IBookingNotificationStep>();
            notificationStep.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<BookingNotificationRecipient>>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((LifecycleStepStatuses.Succeeded, null, null));
            var downstream = new Mock<IDownstreamUpdateService>();
            downstream.Setup(x => x.PublishBookingChangeAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DownstreamUpdateResponse
                {
                    UpdateId = "update-1",
                    BookingId = "booking-1",
                    ChangeType = "Cancel",
                    Status = "Pending",
                    CreatedUtc = FixedNow
                });
            var lifecycle = new Mock<IBookingLifecycleRecorder>();
            lifecycle.Setup(x => x.RecordEventAsync(It.IsAny<BookingLifecycleEventRecord>(), It.IsAny<CancellationToken>()))
                .Callback<BookingLifecycleEventRecord, CancellationToken>((entry, _) => events.Add(entry))
                .ReturnsAsync("event-1");
            lifecycle.Setup(x => x.RecordStepAsync("event-1", It.IsAny<BookingLifecycleStepRecord>(), It.IsAny<CancellationToken>()))
                .Callback<string, BookingLifecycleStepRecord, CancellationToken>((_, step, _) => steps.Add(step))
                .Returns(Task.CompletedTask);

            var sut = new CancellationOrchestrator(
                holds.Object,
                slots.Object,
                transactions.Object,
                uow.Object,
                calendar.Object,
                new StubProfiles("adviser-1", "adviser.one@tenant.com"),
                new StubClock(FixedNow),
                notificationStep.Object,
                downstream.Object,
                lifecycle.Object,
                Mock.Of<ILogger<CancellationOrchestrator>>());

            return new CancellationHarness(sut, calendar, downstream, notificationStep, events, steps);
        }
    }

    private sealed class StubClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class StubProfiles(string adviserId, string mailboxUserId) : IAdviserProfileProjectionRepository
    {
        public Task UpsertRangeAsync(IReadOnlyList<AdviserProfileProjectionRecord> advisers, CancellationToken ct)
            => Task.CompletedTask;

        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListAsync(DateTime? sinceUtc, int take, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AdviserProfileProjectionRecord>>([]);

        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListActiveAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AdviserProfileProjectionRecord>>([]);

        public Task<AdviserProfileProjectionRecord?> GetAsync(string requestedAdviserId, CancellationToken ct)
            => Task.FromResult<AdviserProfileProjectionRecord?>(string.Equals(requestedAdviserId, adviserId, StringComparison.OrdinalIgnoreCase)
                ? new AdviserProfileProjectionRecord
                {
                    AdviserId = adviserId,
                    MailboxUserId = mailboxUserId
                }
                : null);
    }
}
