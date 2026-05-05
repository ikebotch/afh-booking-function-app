using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Bookings;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Domain.Transactions;
using Moq;

namespace AFH.Booking.Tests;

public sealed class LifecycleOrchestratorSequencingTests
{
    [Fact]
    public async Task CancellationOrchestrator_UsesOutlookThenSqlThenNotifications()
    {
        var order = new List<string>();
        var hold = BookingHold.Rehydrate(
            "booking-1",
            "slot-1",
            "user-1",
            BookingHoldStatus.Confirmed,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddMinutes(-30),
            null,
            null,
            null,
            "provider-1");
        var slot = BookingSlot.Rehydrate("slot-1", "tx-1", "adviser-1", "Adviser", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(1), 10, null, null, null, null, null, null, null, DateTime.UtcNow);
        var tx = BookingTransaction.Rehydrate("tx-1", "txn-ref", DateTime.UtcNow, TimeSpan.FromHours(1), "Europe/London", false, "Review", null, BookingTransactionStatus.Open, DateTime.UtcNow, null);

        var holds = new Mock<IBookingHoldRepository>();
        holds.Setup(x => x.GetAsync("booking-1", It.IsAny<CancellationToken>())).ReturnsAsync(hold);
        holds.Setup(x => x.UpdateAsync(It.IsAny<BookingHold>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var slots = new Mock<IBookingSlotRepository>();
        slots.Setup(x => x.GetAsync("slot-1", It.IsAny<CancellationToken>())).ReturnsAsync(slot);

        var txRepo = new Mock<IBookingTransactionRepository>();
        txRepo.Setup(x => x.GetAsync("tx-1", It.IsAny<CancellationToken>())).ReturnsAsync(tx);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var calendar = new Mock<ICalendarGateway>();
        calendar.Setup(x => x.CancelBookingEventAsync("adviser.one@tenant.com", "provider-1", It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("outlook"))
            .Returns(Task.CompletedTask);

        var notifications = new Mock<INotificationService>();
        notifications.Setup(x => x.SendBookingNotificationAsync(It.IsAny<NotificationDispatchRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("notifications"))
            .ReturnsAsync(new NotificationDispatchResponse
            {
                DispatchId = "dispatch-1",
                BookingId = "booking-1",
                EventType = "BookingCancelled",
                SmsRequested = true,
                EmailRequested = true,
                SmsStatus = "Sent",
                EmailStatus = "Composed",
                CreatedUtc = DateTime.UtcNow
            });

        var downstream = new Mock<IDownstreamUpdateService>();
        downstream.Setup(x => x.PublishBookingChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownstreamUpdateResponse { UpdateId = "upd-1", BookingId = "booking-1", ChangeType = "Cancel", Status = "Pending", CreatedUtc = DateTime.UtcNow });

        var audit = new Mock<ILifecycleAuditService>();
        audit.Setup(x => x.RecordEventAsync(It.IsAny<LifecycleAuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("sql"))
            .ReturnsAsync("evt-1");
        audit.Setup(x => x.RecordStepAsync(It.IsAny<LifecycleAuditStepEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var orchestrator = new CancellationOrchestrator(
            holds.Object,
            slots.Object,
            txRepo.Object,
            uow.Object,
            calendar.Object,
            new StubProfiles("adviser-1", "adviser.one@tenant.com"),
            new StubClock(DateTime.UtcNow),
            notifications.Object,
            downstream.Object,
            audit.Object,
            Mock.Of<ILogger<CancellationOrchestrator>>());

        var result = await orchestrator.CancelAsync(
            new CancelBookingCommand
            {
                BookingId = "booking-1",
                RequestedBy = "Client",
                ReasonCode = "ClientCancelled"
            },
            true,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "outlook", "sql", "notifications" }, order);
    }

    [Fact]
    public async Task RearrangementOrchestrator_SequencesCreateConfirmCancelAuditThenNotification()
    {
        var order = new List<string>();
        var oldHold = BookingHold.Rehydrate("booking-old", "slot-old", "user-1", BookingHoldStatus.Confirmed, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(-1), null, null, null, "provider-old");
        var oldSlot = BookingSlot.Rehydrate("slot-old", "tx-1", "adviser-old", "Old Adviser", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(1), 5, null, null, null, null, null, null, null, DateTime.UtcNow);
        var newHold = BookingHold.Rehydrate("booking-new", "slot-new", "user-2", BookingHoldStatus.Confirmed, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(3), DateTime.UtcNow, null, null, null, "provider-new");
        var newSlot = BookingSlot.Rehydrate("slot-new", "tx-1", "adviser-new", "New Adviser", DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(1), 7, null, null, null, null, null, null, null, DateTime.UtcNow);
        var tx = BookingTransaction.Rehydrate("tx-1", "txn-ref", DateTime.UtcNow, TimeSpan.FromHours(1), "Europe/London", false, "Review", null, BookingTransactionStatus.Completed, DateTime.UtcNow, null);

        var holds = new Mock<IBookingHoldRepository>();
        holds.SetupSequence(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldHold)
            .ReturnsAsync(newHold);

        var slots = new Mock<IBookingSlotRepository>();
        slots.Setup(x => x.GetAsync("slot-old", It.IsAny<CancellationToken>())).ReturnsAsync(oldSlot);
        slots.Setup(x => x.GetAsync("slot-new", It.IsAny<CancellationToken>())).ReturnsAsync(newSlot);

        var txRepo = new Mock<IBookingTransactionRepository>();
        txRepo.Setup(x => x.GetAsync("tx-1", It.IsAny<CancellationToken>())).ReturnsAsync(tx);

        var create = new Mock<ICreateBookingHandler>();
        create.Setup(x => x.HandleAsync(It.IsAny<CreateHoldCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("create"))
            .ReturnsAsync(Result<CreateBookingResponse>.Ok(new CreateBookingResponse { BookingId = "booking-new", SlotId = "slot-new", HoldExpiresUtc = DateTime.UtcNow.AddMinutes(3) }));

        var confirm = new Mock<IConfirmBookingHandler>();
        confirm.Setup(x => x.HandleAsync(It.IsAny<ConfirmBookingCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("confirm"))
            .ReturnsAsync(Result<ConfirmBookingResponse>.Ok(new ConfirmBookingResponse { BookingId = "booking-new", SlotId = "slot-new", TransactionId = "tx-1", TransactionRef = "TRX-1", Status = "Confirmed", LifecycleState = "Booked" }));

        var cancel = new Mock<ICancellationOrchestrator>();
        cancel.Setup(x => x.CancelAsync(It.IsAny<CancelBookingCommand>(), false, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("cancel"))
            .ReturnsAsync(Result<CancelBookingResponse>.Ok(new CancelBookingResponse { BookingId = "booking-old", CancelledUtc = DateTime.UtcNow, Status = "Cancelled" }));

        var notifications = new Mock<INotificationService>();
        notifications.Setup(x => x.SendBookingNotificationAsync(It.IsAny<NotificationDispatchRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("notifications"))
            .ReturnsAsync(new NotificationDispatchResponse { DispatchId = "dispatch-1", BookingId = "booking-new", EventType = "BookingRearranged", SmsRequested = true, EmailRequested = true, SmsStatus = "Sent", EmailStatus = "Composed", CreatedUtc = DateTime.UtcNow });

        var downstream = new Mock<IDownstreamUpdateService>();
        downstream.Setup(x => x.PublishBookingChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownstreamUpdateResponse { UpdateId = "upd-1", BookingId = "booking-new", ChangeType = "Rearrange", Status = "Pending", CreatedUtc = DateTime.UtcNow });

        var audit = new Mock<ILifecycleAuditService>();
        audit.Setup(x => x.RecordEventAsync(It.IsAny<LifecycleAuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("sql"))
            .ReturnsAsync("evt-1");
        audit.Setup(x => x.RecordStepAsync(It.IsAny<LifecycleAuditStepEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var orchestrator = new RearrangementOrchestrator(
            holds.Object,
            slots.Object,
            txRepo.Object,
            create.Object,
            confirm.Object,
            cancel.Object,
            notifications.Object,
            downstream.Object,
            audit.Object,
            uow.Object,
            new StubClock(DateTime.UtcNow));

        var result = await orchestrator.RearrangeAsync(
            new RearrangeBookingCommand
            {
                BookingId = "booking-old",
                NewSlotId = "slot-new",
                RequestedBy = "Client",
                ReasonCode = "ClientRearranged"
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "create", "confirm", "cancel", "sql", "notifications" }, order);
    }

    private sealed class StubClock : IClock
    {
        public StubClock(DateTime utcNow)
        {
            UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        }

        public DateTime UtcNow { get; }
    }

    private sealed class StubProfiles : IAdviserProfileProjectionRepository
    {
        private readonly AdviserProfileProjectionRecord _record;

        public StubProfiles(string adviserId, string mailboxUserId)
        {
            _record = new AdviserProfileProjectionRecord
            {
                AdviserId = adviserId,
                DisplayName = adviserId,
                MailboxUserId = mailboxUserId,
                IsActive = true
            };
        }

        public Task UpsertRangeAsync(IReadOnlyList<AdviserProfileProjectionRecord> advisers, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListAsync(DateTime? sinceUtc, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<AdviserProfileProjectionRecord>>([_record]);
        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListActiveAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AdviserProfileProjectionRecord>>([_record]);
        public Task<AdviserProfileProjectionRecord?> GetAsync(string adviserId, CancellationToken ct) => Task.FromResult(string.Equals(_record.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase) ? _record : null);
    }
}
