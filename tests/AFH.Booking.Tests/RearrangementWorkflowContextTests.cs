using System.Text.Json;
using System.Net;
using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Approvals;
using AFH.Booking.Application.Bookings;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Application.Models.Lifecycle;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using Moq;

namespace AFH.Booking.Tests;

public sealed class RearrangementWorkflowContextTests
{
    private static readonly DateTime FixedNow = new(2026, 06, 04, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RearrangeAsync_SelfServiceActorContext_RecordsClientActorAndSelfServiceSource()
    {
        var actor = BookingActorContext.SelfServiceClient("client-1", "corr-self");
        var harness = RearrangementHarness.Create("slot-assigned", "adviser-old");

        var result = await harness.Sut.RearrangeAsync(new RearrangeBookingCommand
        {
            BookingId = "booking-old",
            NewSlotId = "slot-assigned",
            ActorContext = actor,
            ReasonCode = "CLIENT_RESCHEDULE"
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var recorded = Assert.Single(harness.Events);
        Assert.Same(actor, recorded.ActorContext);
        Assert.Equal(LifecycleActors.Client, recorded.ActorType);
        Assert.Equal("client-1", recorded.ActorId);
        Assert.Equal("corr-self", recorded.CorrelationId);
        Assert.Equal(BookingActorContext.SourceSelfService, recorded.SourceSystem);
    }

    [Fact]
    public async Task RearrangeAsync_PartnerActorContext_RecordsPartnerActorAndSource()
    {
        var actor = BookingActorContext.Partner("PartnerCo", "partner-user", "Partner User", "corr-partner");
        var harness = RearrangementHarness.Create("slot-assigned", "adviser-old");

        var result = await harness.Sut.RearrangeAsync(new RearrangeBookingCommand
        {
            BookingId = "booking-old",
            NewSlotId = "slot-assigned",
            ActorContext = actor,
            ReasonCode = "PARTNER_REQUEST"
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var recorded = Assert.Single(harness.Events);
        Assert.Equal(LifecycleActors.Partner, recorded.ActorType);
        Assert.Equal("partner-user", recorded.ActorId);
        Assert.Equal("PartnerCo", recorded.PartnerName);
        Assert.Equal("corr-partner", recorded.CorrelationId);
        Assert.Equal(BookingActorContext.SourcePartner, recorded.SourceSystem);
    }

    [Fact]
    public async Task RearrangeAsync_InternalAdminActorContext_DoesNotRecordClientByDefault()
    {
        var actor = BookingActorContext.InternalAdmin("admin-1", "Admin One", "corr-admin");
        var harness = RearrangementHarness.Create("slot-assigned", "adviser-old");

        var result = await harness.Sut.RearrangeAsync(new RearrangeBookingCommand
        {
            BookingId = "booking-old",
            NewSlotId = "slot-assigned",
            ActorContext = actor,
            ReasonCode = "ADMIN_REQUEST"
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var recorded = Assert.Single(harness.Events);
        Assert.Equal(BookingActorContext.ActorInternalAdmin, recorded.ActorType);
        Assert.NotEqual(LifecycleActors.Client, recorded.ActorType);
        Assert.Equal("admin-1", recorded.ActorId);
        Assert.Equal(BookingActorContext.SourceInternalAdmin, recorded.SourceSystem);
    }

    [Fact]
    public async Task RearrangeAsync_LegacyCommandWithoutActorContext_UsesLegacyFields()
    {
        var harness = RearrangementHarness.Create("slot-assigned", "adviser-old");

        var result = await harness.Sut.RearrangeAsync(new RearrangeBookingCommand
        {
            BookingId = "booking-old",
            NewSlotId = "slot-assigned",
            RequestedBy = LifecycleActors.Client,
            ActorId = "legacy-client",
            CorrelationId = "legacy-corr",
            ReasonCode = "CLIENT_RESCHEDULE"
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var recorded = Assert.Single(harness.Events);
        Assert.Null(recorded.ActorContext);
        Assert.Equal(LifecycleActors.Client, recorded.ActorType);
        Assert.Equal("legacy-client", recorded.ActorId);
        Assert.Equal("legacy-corr", recorded.CorrelationId);
        Assert.Equal("BookingService", recorded.SourceSystem);
    }

    [Theory]
    [InlineData("slot-assigned", "adviser-old")]
    [InlineData("slot-alternative", "adviser-new")]
    public async Task RearrangeAsync_SelectedOptionSlot_ResolvesOptionTransactionInternallyUsingOnlyNewSlotId(
        string newSlotId,
        string newAdviserId)
    {
        var actor = BookingActorContext.SelfServiceClient("client-1", "corr-self");
        var harness = RearrangementHarness.Create(newSlotId, newAdviserId);

        var result = await harness.Sut.RearrangeAsync(new RearrangeBookingCommand
        {
            BookingId = "booking-old",
            NewSlotId = newSlotId,
            ActorContext = actor,
            ReasonCode = "CLIENT_RESCHEDULE"
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(newSlotId, harness.LastCreateHoldCommand?.SlotId);
        Assert.Equal("tx-original", harness.LastCreateHoldCommand?.TransactionRef);
        Assert.Equal(newSlotId, result.Value!.NewSlotId);
        Assert.Equal(newAdviserId, result.Value.NewAdviserId);
    }

    [Fact]
    public async Task RearrangeAsync_ReplacementHoldFailure_DoesNotCancelOldBooking()
    {
        var harness = RearrangementHarness.Create(
            "slot-assigned",
            "adviser-old",
            createResult: Result<CreateBookingResponse>.Fail(
                HttpStatusCode.Conflict,
                "Slot no longer available.",
                Errors.SlotNoLongerAvailable));

        var result = await harness.Sut.RearrangeAsync(new RearrangeBookingCommand
        {
            BookingId = "booking-old",
            NewSlotId = "slot-assigned",
            ActorContext = BookingActorContext.SelfServiceClient("client-1", "corr-self"),
            ReasonCode = "CLIENT_RESCHEDULE"
        }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(Errors.SlotNoLongerAvailable, result.ErrorCode);
        harness.Cancel.Verify(x => x.CancelAsync(
            It.IsAny<CancelBookingCommand>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(harness.Events);
    }

    [Fact]
    public async Task RearrangeAsync_DuplicateSubmit_ReturnsExistingResultWithoutCreatingReplacementBooking()
    {
        var afterJson = JsonSerializer.Serialize(new
        {
            previousBookingId = "booking-old",
            newBookingId = "booking-new",
            newSlotId = "slot-assigned",
            previousAdviserId = "adviser-old",
            previousAdviserName = "Old Adviser",
            previousStartUtc = FixedNow.AddDays(1),
            previousEndUtc = FixedNow.AddDays(1).AddHours(1),
            newAdviserId = "adviser-old",
            newAdviserName = "Old Adviser",
            newStartUtc = FixedNow.AddDays(2),
            newEndUtc = FixedNow.AddDays(2).AddHours(1),
            notificationSummary = "Existing rearrangement result."
        });
        var existing = new LifecycleEventRecord
        {
            Id = "event-existing",
            BookingId = "booking-new",
            RelatedBookingId = "booking-old",
            EventType = LifecycleEventTypes.Rearranged,
            AfterJson = afterJson,
            OccurredUtc = FixedNow,
            TriggerReason = BookingWorkflowIdempotencyKeys.Rearrangement("booking-old", "slot-assigned", LifecycleActors.Client)
        };
        var harness = RearrangementHarness.Create("slot-assigned", "adviser-old", existingWorkflow: existing);

        var result = await harness.Sut.RearrangeAsync(new RearrangeBookingCommand
        {
            BookingId = "booking-old",
            NewSlotId = "slot-assigned",
            RequestedBy = LifecycleActors.Client,
            ReasonCode = "CLIENT_RESCHEDULE"
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("booking-new", result.Value!.NewBookingId);
        Assert.Equal("slot-assigned", result.Value.NewSlotId);
        Assert.Equal("Existing rearrangement result.", result.Value.NotificationSummary);
        Assert.Null(harness.LastCreateHoldCommand);
        harness.Cancel.Verify(x => x.CancelAsync(
            It.IsAny<CancelBookingCommand>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
        harness.Downstream.Verify(x => x.PublishBookingChangeAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(harness.Events);
    }

    [Fact]
    public async Task RearrangeAsync_NotificationAndDownstreamBehaviour_RemainsUnchanged()
    {
        var harness = RearrangementHarness.Create("slot-assigned", "adviser-old");

        var result = await harness.Sut.RearrangeAsync(new RearrangeBookingCommand
        {
            BookingId = "booking-old",
            NewSlotId = "slot-assigned",
            ActorContext = BookingActorContext.InternalAdmin("admin-1"),
            ReasonCode = "ADMIN_REQUEST"
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        harness.Notifications.Verify(x => x.RequestAsync(
            It.Is<BookingWorkflowNotificationRequest>(request =>
                request.LifecycleEventType == LifecycleEventTypes.Rearranged &&
                request.CorrelationId == "booking-new" &&
                request.ActorType == BookingActorContext.ActorInternalAdmin),
            It.IsAny<CancellationToken>()), Times.Once);
        harness.Downstream.Verify(x => x.PublishBookingChangeAsync(
            "booking-new",
            "Rearrange",
            "txn-ref",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
        harness.Notifications.Verify(x => x.RequestAsync(
            It.Is<BookingWorkflowNotificationRequest>(request =>
                request.Data["IdempotencyKey"] == "booking-rescheduled:booking-new"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApprovalReview_RearrangeExecution_PreservesReviewerActorSourceAndRequestId()
    {
        RearrangeBookingCommand? capturedRearrange = null;
        var row = new ApprovalWorkflowRecord
        {
            Id = "approval-1",
            BookingId = "booking-old",
            TransactionId = "tx-original",
            ChangeType = "Rearrange",
            RequestedBy = LifecycleActors.Adviser,
            RequesterId = "adviser-1",
            Status = "Pending",
            RequestedUtc = FixedNow.AddMinutes(-5),
            ReasonCode = "ADVISER_REQUEST",
            ReasonDetail = "Needs manager approval",
            RequestedPayloadJson = """{"newSlotId":"slot-assigned"}""",
            ApproverTargetDisplayName = "Manager"
        };
        var store = new Mock<IApprovalWorkflowStore>();
        store.Setup(x => x.GetForUpdateAsync("approval-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);
        store.Setup(x => x.LoadBookingAsync("booking-old", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApprovalBooking());
        var rearrangement = new Mock<IRearrangementOrchestrator>();
        rearrangement.Setup(x => x.RearrangeAsync(It.IsAny<RearrangeBookingCommand>(), It.IsAny<CancellationToken>()))
            .Callback<RearrangeBookingCommand, CancellationToken>((cmd, _) => capturedRearrange = cmd)
            .ReturnsAsync(Result<RearrangeBookingResponse>.Ok(new RearrangeBookingResponse
            {
                PreviousBookingId = "booking-old",
                NewBookingId = "booking-new",
                NewSlotId = "slot-assigned",
                PreviousAdviserId = "adviser-1",
                PreviousAdviserName = "Adviser One",
                PreviousStartUtc = FixedNow.AddDays(1),
                PreviousEndUtc = FixedNow.AddDays(1).AddHours(1),
                NewAdviserId = "adviser-1",
                NewAdviserName = "Adviser One",
                NewStartUtc = FixedNow.AddDays(2),
                NewEndUtc = FixedNow.AddDays(2).AddHours(1),
                NotificationSummary = "Rescheduled"
            }));
        var audit = new Mock<ILifecycleAuditService>();
        audit.Setup(x => x.RecordEventAsync(It.IsAny<LifecycleAuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("approval-event-1");
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var sut = new ApprovalWorkflowService(
            store.Object,
            Mock.Of<IBookingHoldRepository>(),
            Mock.Of<IReleaseHoldService>(),
            Mock.Of<IApprovalRoutingService>(),
            Mock.Of<ICancellationOrchestrator>(),
            rearrangement.Object,
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
        Assert.NotNull(capturedRearrange);
        Assert.Equal("approval-1", capturedRearrange!.ApprovalRequestId);
        Assert.Equal("slot-assigned", capturedRearrange.NewSlotId);
        Assert.Equal(BookingActorContext.SourceApprovalWorkflow, capturedRearrange.ActorContext?.SourceApplication);
        Assert.Equal(BookingActorContext.ActorManager, capturedRearrange.ActorContext?.ActorType);
        Assert.Equal("manager-1", capturedRearrange.ActorContext?.ActorId);
        Assert.Equal("corr-approval", capturedRearrange.ActorContext?.CorrelationId);
    }

    private static ApprovalBookingSnapshot ApprovalBooking()
    {
        var hold = BookingHold.Rehydrate(
            "booking-old",
            "slot-old",
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
            "slot-old",
            "tx-original",
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
            "tx-original",
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

    private sealed class RearrangementHarness
    {
        private RearrangementHarness(
            RearrangementOrchestrator sut,
            Mock<ICancellationOrchestrator> cancel,
            Mock<IBookingWorkflowNotificationAdapter> notifications,
            Mock<IDownstreamUpdateService> downstream,
            List<BookingLifecycleEventRecord> events,
            Func<CreateHoldCommand?> getLastCreateHoldCommand)
        {
            Sut = sut;
            Cancel = cancel;
            Notifications = notifications;
            Downstream = downstream;
            Events = events;
            _getLastCreateHoldCommand = getLastCreateHoldCommand;
        }

        private readonly Func<CreateHoldCommand?> _getLastCreateHoldCommand;

        public RearrangementOrchestrator Sut { get; }
        public Mock<ICancellationOrchestrator> Cancel { get; }
        public Mock<IBookingWorkflowNotificationAdapter> Notifications { get; }
        public Mock<IDownstreamUpdateService> Downstream { get; }
        public List<BookingLifecycleEventRecord> Events { get; }
        public CreateHoldCommand? LastCreateHoldCommand => _getLastCreateHoldCommand();

        public static RearrangementHarness Create(
            string newSlotId,
            string newAdviserId,
            Result<CreateBookingResponse>? createResult = null,
            LifecycleEventRecord? existingWorkflow = null)
        {
            var events = new List<BookingLifecycleEventRecord>();
            var oldHold = BookingHold.Rehydrate(
                "booking-old",
                "slot-old",
                "user-1",
                BookingHoldStatus.Confirmed,
                FixedNow.AddHours(-2),
                FixedNow.AddHours(1),
                FixedNow.AddHours(-1),
                null,
                null,
                null,
                "provider-old",
                null);
            var newHold = BookingHold.Rehydrate(
                "booking-new",
                newSlotId,
                "user-2",
                BookingHoldStatus.Confirmed,
                FixedNow,
                FixedNow.AddMinutes(5),
                FixedNow,
                null,
                null,
                null,
                "provider-new",
                null);
            var oldSlot = BookingSlot.Rehydrate(
                "slot-old",
                "tx-original",
                "adviser-old",
                "Old Adviser",
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
            var selectedSlot = BookingSlot.Rehydrate(
                newSlotId,
                $"option-tx-{newSlotId}",
                newAdviserId,
                newAdviserId == "adviser-old" ? "Old Adviser" : "New Adviser",
                FixedNow.AddDays(2),
                FixedNow.AddDays(2).AddHours(1),
                5,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                FixedNow);
            var originalTx = BookingTransaction.Rehydrate(
                "tx-original",
                "txn-ref",
                FixedNow,
                TimeSpan.FromHours(1),
                "Europe/London",
                false,
                "Review",
                null,
                BookingTransactionStatus.Completed,
                FixedNow,
                null);
            var optionTx = BookingTransaction.Rehydrate(
                $"option-tx-{newSlotId}",
                "tx-original",
                FixedNow,
                TimeSpan.FromHours(1),
                "Europe/London",
                false,
                "Review",
                null,
                BookingTransactionStatus.Open,
                FixedNow,
                FixedNow.AddMinutes(10));

            var holds = new Mock<IBookingHoldRepository>();
            holds.Setup(x => x.GetAsync("booking-old", It.IsAny<CancellationToken>()))
                .ReturnsAsync(oldHold);
            holds.Setup(x => x.GetAsync("booking-new", It.IsAny<CancellationToken>()))
                .ReturnsAsync(newHold);
            var slots = new Mock<IBookingSlotRepository>();
            slots.Setup(x => x.GetAsync("slot-old", It.IsAny<CancellationToken>()))
                .ReturnsAsync(oldSlot);
            slots.Setup(x => x.GetAsync(newSlotId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(selectedSlot);
            var transactions = new Mock<IBookingTransactionRepository>();
            transactions.Setup(x => x.GetAsync("tx-original", It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalTx);
            transactions.Setup(x => x.GetAsync($"option-tx-{newSlotId}", It.IsAny<CancellationToken>()))
                .ReturnsAsync(optionTx);
            CreateHoldCommand? lastCreate = null;
            var create = new Mock<ICreateBookingService>();
            create.Setup(x => x.HandleAsync(It.IsAny<CreateHoldCommand>(), It.IsAny<CancellationToken>()))
                .Callback<CreateHoldCommand, CancellationToken>((cmd, _) => lastCreate = cmd)
                .ReturnsAsync(createResult ?? Result<CreateBookingResponse>.Ok(new CreateBookingResponse
                {
                    BookingId = "booking-new",
                    SlotId = newSlotId,
                    HoldExpiresUtc = FixedNow.AddMinutes(5),
                    CompanyBufferMinutes = 0
                }));
            var confirm = new Mock<IConfirmBookingService>();
            confirm.Setup(x => x.HandleAsync(It.IsAny<ConfirmBookingCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ConfirmBookingResponse>.Ok(new ConfirmBookingResponse
                {
                    BookingId = "booking-new",
                    SlotId = newSlotId,
                    TransactionId = "tx-original",
                    TransactionRef = "txn-ref",
                    Status = "Confirmed",
                    LifecycleState = LifecycleEventTypes.Booked
                }));
            var cancel = new Mock<ICancellationOrchestrator>();
            cancel.Setup(x => x.CancelAsync(It.IsAny<CancelBookingCommand>(), false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<CancelBookingResponse>.Ok(new CancelBookingResponse
                {
                    BookingId = "booking-old",
                    CancelledUtc = FixedNow,
                    Status = "Cancelled"
                }));
            var notifications = new Mock<IBookingWorkflowNotificationAdapter>();
            notifications.Setup(x => x.RequestAsync(
                    It.IsAny<BookingWorkflowNotificationRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(BookingWorkflowNotificationOutcome.Succeeded("BookingRescheduled", 0));
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
                    BookingId = "booking-new",
                    ChangeType = "Rearrange",
                    Status = "Pending",
                    CreatedUtc = FixedNow
                });
            var lifecycle = new Mock<IBookingLifecycleRecorder>();
            lifecycle.Setup(x => x.RecordEventAsync(It.IsAny<BookingLifecycleEventRecord>(), It.IsAny<CancellationToken>()))
                .Callback<BookingLifecycleEventRecord, CancellationToken>((entry, _) => events.Add(entry))
                .ReturnsAsync("event-1");
            lifecycle.Setup(x => x.RecordStepAsync("event-1", It.IsAny<BookingLifecycleStepRecord>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var uow = new Mock<IUnitOfWork>();
            uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            var idempotency = new Mock<IBookingWorkflowIdempotencyGuard>();
            idempotency.Setup(x => x.FindCompletedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LifecycleEventRecord?)null);
            if (existingWorkflow is not null)
            {
                idempotency.Setup(x => x.FindCompletedAsync(existingWorkflow.TriggerReason!, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(existingWorkflow);
            }
            var sut = new RearrangementOrchestrator(
                holds.Object,
                slots.Object,
                transactions.Object,
                create.Object,
                confirm.Object,
                cancel.Object,
                notifications.Object,
                downstream.Object,
                lifecycle.Object,
                idempotency.Object,
                uow.Object,
                new StubClock(FixedNow));

            return new RearrangementHarness(sut, cancel, notifications, downstream, events, () => lastCreate);
        }
    }

    private sealed class StubClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
