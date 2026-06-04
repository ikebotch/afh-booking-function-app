using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AFH.Booking.Application.Abstractions.Bookings.Holds;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.Holds;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Models.Lifecycle;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AFH.Booking.Tests;

public class CreateBookingServiceTests
{
    private readonly Mock<IBookingContextLoader> _loader;
    private readonly Mock<IBookingHoldService> _holdService;
    private readonly Mock<IBookingCalendarService> _calendarService;
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IClock> _clock;
    private readonly Mock<IBookingLifecycleRecorder> _lifecycle;
    private readonly Mock<IBookingWorkflowNotificationAdapter> _notifications;
    private readonly CreateBookingService _sut;

    private static readonly DateTime FixedNow = new DateTime(2026, 03, 25, 10, 0, 0, DateTimeKind.Utc);

    public CreateBookingServiceTests()
    {
        _loader = new Mock<IBookingContextLoader>();
        _holdService = new Mock<IBookingHoldService>();
        _calendarService = new Mock<IBookingCalendarService>();
        _uow = new Mock<IUnitOfWork>();
        _clock = new Mock<IClock>();
        _lifecycle = new Mock<IBookingLifecycleRecorder>();
        _notifications = new Mock<IBookingWorkflowNotificationAdapter>();

        _clock.Setup(c => c.UtcNow).Returns(FixedNow);
        _lifecycle.Setup(x => x.RecordEventAsync(It.IsAny<BookingLifecycleEventRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("hold-created-event-id");
        _notifications.Setup(x => x.RequestAsync(
                It.IsAny<BookingWorkflowNotificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingWorkflowNotificationOutcome.Succeeded("BookingHoldCreated", 0));

        _sut = new CreateBookingService(
            _loader.Object,
            _holdService.Object,
            _calendarService.Object,
            _uow.Object,
            _clock.Object,
            _lifecycle.Object,
            _notifications.Object,
            NullLogger<CreateBookingService>.Instance);
    }

    private static BookingContext MakeContext(string slotId = "slot-1", string txId = "tx-1")
    {
        var tx = BookingTransaction.Rehydrate(txId, txId, DateTime.UtcNow.AddHours(2),
            TimeSpan.FromHours(1), "UTC", false, "Meeting", null,
            BookingTransactionStatus.Open, DateTime.UtcNow, DateTime.UtcNow.AddHours(2));
        var slot = BookingSlot.Rehydrate(slotId, txId, "adv", "Adviser Name",
            DateTime.UtcNow.AddHours(2), DateTime.UtcNow.AddHours(3),
            5, null, null, 0, 0, null, null, null, DateTime.UtcNow);
        return new BookingContext(slot, tx, "cal-user@tenant.com");
    }

    private static BookingHold MakeHold(string id = "hold-1", string slotId = "slot-1")
        => BookingHold.Rehydrate(id, slotId, "adv", BookingHoldStatus.Active,
            DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null, null, null, null, null, null);

    // -------------------------------------------------------------------------
    // Happy path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_Success_LoadsContext_CreatesHold_SendsCalendarEvent_SavesAndReturnsBookingId()
    {
        var cmd = new CreateHoldCommand { SlotId = "slot-1", TransactionRef = "tx-1" };
        var context = MakeContext();
        var hold = MakeHold();

        _loader.Setup(l => l.LoadAsync(cmd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BookingContext>.Ok(context));

        _holdService.Setup(h => h.CreateOrReplaceAsync(context, FixedNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BookingHold>.Ok(hold));

        _calendarService.Setup(c => c.CreateHoldEventAsync(context, hold, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit>.Ok(Unit.Value));

        var result = await _sut.HandleAsync(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        // BookingId is the hold's Id (not HoldId — the response property is BookingId)
        Assert.Equal("hold-1", result.Value!.BookingId);
        Assert.Equal("slot-1", result.Value!.SlotId);

        _loader.Verify(l => l.LoadAsync(cmd, It.IsAny<CancellationToken>()), Times.Once);
        _holdService.Verify(h => h.CreateOrReplaceAsync(context, FixedNow, It.IsAny<CancellationToken>()), Times.Once);
        _calendarService.Verify(c => c.CreateHoldEventAsync(context, hold, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_Success_RecordsHoldCreatedLifecycleEventAndOrderedSteps()
    {
        var actor = BookingActorContext.SelfServiceClient("client-1", "corr-1");
        var cmd = new CreateHoldCommand
        {
            SlotId = "slot-1",
            TransactionRef = "tx-1",
            ActorContext = actor
        };
        var context = MakeContext();
        var hold = MakeHold();
        BookingLifecycleEventRecord? lifecycleEvent = null;
        var lifecycleSteps = new List<BookingLifecycleStepRecord>();

        _loader.Setup(l => l.LoadAsync(cmd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BookingContext>.Ok(context));
        _holdService.Setup(h => h.CreateOrReplaceAsync(context, FixedNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BookingHold>.Ok(hold));
        _calendarService.Setup(c => c.CreateHoldEventAsync(context, hold, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit>.Ok(Unit.Value));
        _notifications.Setup(x => x.RequestAsync(
                It.Is<BookingWorkflowNotificationRequest>(request =>
                    request.LifecycleEventType == LifecycleEventTypes.HoldCreated &&
                    request.CorrelationId == hold.Id &&
                    request.ActorType == LifecycleActors.Client),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingWorkflowNotificationOutcome.Skipped(
                "BookingHoldCreated",
                BookingWorkflowNotificationOutcomeStatuses.SkippedPolicyDisabled,
                0));
        _lifecycle.Setup(x => x.RecordEventAsync(It.IsAny<BookingLifecycleEventRecord>(), It.IsAny<CancellationToken>()))
            .Callback<BookingLifecycleEventRecord, CancellationToken>((entry, _) => lifecycleEvent = entry)
            .ReturnsAsync("hold-created-event-id");
        _lifecycle.Setup(x => x.RecordStepAsync(
                "hold-created-event-id",
                It.IsAny<BookingLifecycleStepRecord>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, BookingLifecycleStepRecord, CancellationToken>((_, step, _) => lifecycleSteps.Add(step))
            .Returns(Task.CompletedTask);

        var result = await _sut.HandleAsync(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("hold-1", result.Value!.BookingId);
        Assert.Equal(LifecycleEventTypes.HoldCreated, lifecycleEvent?.EventType);
        Assert.Equal("hold-1", lifecycleEvent?.BookingId);
        Assert.Equal("tx-1", lifecycleEvent?.TransactionId);
        Assert.Same(actor, lifecycleEvent?.ActorContext);
        Assert.Null(lifecycleEvent?.NewState);

        Assert.Collection(lifecycleSteps,
            step =>
            {
                Assert.Equal(LifecycleStepNames.Outlook, step.StepName);
                Assert.Equal(1, step.Sequence);
                Assert.Equal(LifecycleStepStatuses.Succeeded, step.Status);
                Assert.Same(actor, step.ActorContext);
            },
            step =>
            {
                Assert.Equal(LifecycleStepNames.SqlAudit, step.StepName);
                Assert.Equal(2, step.Sequence);
                Assert.Equal(LifecycleStepStatuses.Succeeded, step.Status);
                Assert.Same(actor, step.ActorContext);
            },
            step =>
            {
                Assert.Equal(LifecycleStepNames.Notifications, step.StepName);
                Assert.Equal(3, step.Sequence);
                Assert.Equal(LifecycleStepStatuses.Skipped, step.Status);
                Assert.Same(actor, step.ActorContext);
            });
    }

    [Fact]
    public async Task HandleAsync_WhenActorContextIsNull_RecordsLegacySystemFallback()
    {
        var cmd = new CreateHoldCommand { SlotId = "slot-1", TransactionRef = "tx-1" };
        var context = MakeContext();
        var hold = MakeHold();
        BookingLifecycleEventRecord? lifecycleEvent = null;

        _loader.Setup(l => l.LoadAsync(cmd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BookingContext>.Ok(context));
        _holdService.Setup(h => h.CreateOrReplaceAsync(context, FixedNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BookingHold>.Ok(hold));
        _calendarService.Setup(c => c.CreateHoldEventAsync(context, hold, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit>.Ok(Unit.Value));
        _lifecycle.Setup(x => x.RecordEventAsync(It.IsAny<BookingLifecycleEventRecord>(), It.IsAny<CancellationToken>()))
            .Callback<BookingLifecycleEventRecord, CancellationToken>((entry, _) => lifecycleEvent = entry)
            .ReturnsAsync("hold-created-event-id");

        var result = await _sut.HandleAsync(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(lifecycleEvent?.ActorContext);
        Assert.Equal(LifecycleActors.System, lifecycleEvent?.ActorType);
        Assert.Equal("BookingService", lifecycleEvent?.SourceSystem);
    }

    [Fact]
    public async Task HandleAsync_WhenHoldCreatedNotificationFails_StillSucceedsAndRecordsFailedNotificationStep()
    {
        var cmd = new CreateHoldCommand { SlotId = "slot-1", TransactionRef = "tx-1" };
        var context = MakeContext();
        var hold = MakeHold();
        var lifecycleSteps = new List<BookingLifecycleStepRecord>();

        _loader.Setup(l => l.LoadAsync(cmd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BookingContext>.Ok(context));
        _holdService.Setup(h => h.CreateOrReplaceAsync(context, FixedNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BookingHold>.Ok(hold));
        _calendarService.Setup(c => c.CreateHoldEventAsync(context, hold, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit>.Ok(Unit.Value));
        _notifications.Setup(x => x.RequestAsync(
                It.IsAny<BookingWorkflowNotificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("notification policy unavailable"));
        _lifecycle.Setup(x => x.RecordStepAsync(
                "hold-created-event-id",
                It.IsAny<BookingLifecycleStepRecord>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, BookingLifecycleStepRecord, CancellationToken>((_, step, _) => lifecycleSteps.Add(step))
            .Returns(Task.CompletedTask);

        var result = await _sut.HandleAsync(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var notificationStep = Assert.Single(lifecycleSteps, x => x.StepName == LifecycleStepNames.Notifications);
        Assert.Equal(LifecycleStepStatuses.Failed, notificationStep.Status);
        Assert.Equal(LifecycleErrorCodes.NotificationFailed, notificationStep.ErrorCode);
    }

    // -------------------------------------------------------------------------
    // Failure paths — each short-circuits without calling downstream services
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_WhenContextLoadFails_ReturnsFailureAndDoesNotCreateHold()
    {
        var cmd = new CreateHoldCommand { SlotId = "slot-1", TransactionRef = "tx-1" };

        _loader.Setup(l => l.LoadAsync(cmd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BookingContext>.Fail(HttpStatusCode.NotFound, "Slot not found"));

        var result = await _sut.HandleAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal("Slot not found", result.ErrorMessage);

        _holdService.Verify(h => h.CreateOrReplaceAsync(
            It.IsAny<BookingContext>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenHoldServiceFails_ReturnsFailureAndDoesNotSave()
    {
        var cmd = new CreateHoldCommand { SlotId = "slot-1", TransactionRef = "tx-1" };
        var context = MakeContext();

        _loader.Setup(l => l.LoadAsync(cmd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BookingContext>.Ok(context));

        _holdService.Setup(h => h.CreateOrReplaceAsync(context, FixedNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BookingHold>.Fail(HttpStatusCode.Conflict, "Slot already held"));

        var result = await _sut.HandleAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal("Slot already held", result.ErrorMessage);

        _calendarService.Verify(c => c.CreateHoldEventAsync(
            It.IsAny<BookingContext>(), It.IsAny<BookingHold>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCalendarServiceFails_ReturnsFailureAndDoesNotSave()
    {
        var cmd = new CreateHoldCommand { SlotId = "slot-1", TransactionRef = "tx-1" };
        var context = MakeContext();
        var hold = MakeHold();

        _loader.Setup(l => l.LoadAsync(cmd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BookingContext>.Ok(context));

        _holdService.Setup(h => h.CreateOrReplaceAsync(context, FixedNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BookingHold>.Ok(hold));

        _calendarService.Setup(c => c.CreateHoldEventAsync(context, hold, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit>.Fail(HttpStatusCode.BadGateway, "Calendar unavailable"));

        var result = await _sut.HandleAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadGateway, result.StatusCode);
        Assert.Equal("Calendar unavailable", result.ErrorMessage);

        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
