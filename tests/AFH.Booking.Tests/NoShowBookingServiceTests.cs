using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Bookings;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.Models.Lifecycle;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using Moq;

namespace AFH.Booking.Tests;

public sealed class NoShowBookingServiceTests
{
    [Fact]
    public async Task HandleAsync_RecordsNoShowLifecycleEventForConfirmedBooking()
    {
        var now = new DateTime(2026, 05, 05, 10, 30, 00, DateTimeKind.Utc);
        var hold = BookingHold.Rehydrate(
            "booking-1",
            "slot-1",
            "user-1",
            BookingHoldStatus.Confirmed,
            now.AddHours(-2),
            now.AddHours(1),
            now.AddHours(-1),
            null,
            null,
            null,
            "event-1", null);
        var slot = BookingSlot.Rehydrate(
            "slot-1",
            "tx-1",
            "adviser-1",
            "Adviser One",
            now.AddHours(-1),
            now.AddMinutes(-30),
            5,
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
            "txn-ref",
            slot.StartUtc,
            TimeSpan.FromMinutes(30),
            "Europe/London",
            false,
            "Review",
            null,
            BookingTransactionStatus.Completed,
            now.AddHours(-3),
            null);
        LifecycleAuditEntry? auditEntry = null;

        var holds = new Mock<IBookingHoldRepository>();
        holds.Setup(x => x.GetAsync("booking-1", It.IsAny<CancellationToken>())).ReturnsAsync(hold);
        var slots = new Mock<IBookingSlotRepository>();
        slots.Setup(x => x.GetAsync("slot-1", It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        var transactions = new Mock<IBookingTransactionRepository>();
        transactions.Setup(x => x.GetAsync("tx-1", It.IsAny<CancellationToken>())).ReturnsAsync(tx);
        var audit = new Mock<ILifecycleAuditService>();
        audit.Setup(x => x.RecordEventAsync(It.IsAny<LifecycleAuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LifecycleAuditEntry, CancellationToken>((entry, _) => auditEntry = entry)
            .ReturnsAsync("event-id-1");
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        INoShowBookingService sut = new NoShowBookingService(
            holds.Object,
            slots.Object,
            transactions.Object,
            new BookingLifecycleRecorder(audit.Object),
            Mock.Of<IBookingWorkflowIdempotencyGuard>(),
            uow.Object,
            new StubClock(now));

        var result = await sut.HandleAsync(new RecordNoShowCommand
        {
            BookingId = "booking-1",
            RequestedBy = LifecycleActors.System,
            ActorId = "system",
            CorrelationId = "corr-1"
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("event-id-1", result.Value?.LifecycleEventId);
        Assert.NotNull(auditEntry);
        Assert.Equal(LifecycleEventTypes.NoShow, auditEntry!.EventType);
        Assert.Equal(LifecycleStates.Booked, auditEntry.PreviousState);
        Assert.Equal(LifecycleStates.NoShow, auditEntry.NewState);
        Assert.Equal(LifecycleActors.System, auditEntry.ActorType);
        Assert.Equal(now, auditEntry.OccurredUtc);
        audit.Verify(x => x.RecordStepAsync(
            It.Is<LifecycleAuditStepEntry>(step => step.LifecycleEventId == "event-id-1" && step.StepName == LifecycleStepNames.SqlAudit),
            It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DuplicateNoShow_ReturnsExistingLifecycleEventWithoutRecordingAgain()
    {
        var now = new DateTime(2026, 05, 05, 10, 30, 00, DateTimeKind.Utc);
        var existing = new LifecycleEventRecord
        {
            Id = "event-existing",
            BookingId = "booking-1",
            TransactionId = "tx-1",
            EventType = LifecycleEventTypes.NoShow,
            PreviousState = LifecycleStates.Booked,
            NewState = LifecycleStates.NoShow,
            OccurredUtc = now.AddMinutes(-2),
            TriggerReason = BookingWorkflowIdempotencyKeys.NoShow("booking-1", LifecycleActors.System)
        };
        var idempotency = new Mock<IBookingWorkflowIdempotencyGuard>();
        idempotency.Setup(x => x.FindCompletedAsync(existing.TriggerReason!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var holds = new Mock<IBookingHoldRepository>();
        var audit = new Mock<ILifecycleAuditService>();

        INoShowBookingService sut = new NoShowBookingService(
            holds.Object,
            Mock.Of<IBookingSlotRepository>(),
            Mock.Of<IBookingTransactionRepository>(),
            new BookingLifecycleRecorder(audit.Object),
            idempotency.Object,
            Mock.Of<IUnitOfWork>(),
            new StubClock(now));

        var result = await sut.HandleAsync(new RecordNoShowCommand
        {
            BookingId = "booking-1",
            RequestedBy = LifecycleActors.System
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("event-existing", result.Value?.LifecycleEventId);
        holds.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        audit.Verify(x => x.RecordEventAsync(It.IsAny<LifecycleAuditEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_BlocksNoShowForUnconfirmedBooking()
    {
        var now = DateTime.UtcNow;
        var hold = BookingHold.Rehydrate(
            "booking-1",
            "slot-1",
            "user-1",
            BookingHoldStatus.Active,
            now.AddHours(-1),
            now.AddHours(1),
            null,
            null,
            null,
            null,
            null, null);
        var holds = new Mock<IBookingHoldRepository>();
        holds.Setup(x => x.GetAsync("booking-1", It.IsAny<CancellationToken>())).ReturnsAsync(hold);
        var audit = new Mock<ILifecycleAuditService>();

        INoShowBookingService sut = new NoShowBookingService(
            holds.Object,
            Mock.Of<IBookingSlotRepository>(),
            Mock.Of<IBookingTransactionRepository>(),
            new BookingLifecycleRecorder(audit.Object),
            Mock.Of<IBookingWorkflowIdempotencyGuard>(),
            Mock.Of<IUnitOfWork>(),
            new StubClock(now));

        var result = await sut.HandleAsync(new RecordNoShowCommand
        {
            BookingId = "booking-1",
            RequestedBy = LifecycleActors.System
        }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(Errors.Conflict, result.ErrorCode);
        audit.Verify(x => x.RecordEventAsync(It.IsAny<LifecycleAuditEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class StubClock : IClock
    {
        public StubClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
