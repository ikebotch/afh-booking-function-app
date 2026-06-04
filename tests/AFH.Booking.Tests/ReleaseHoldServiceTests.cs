using System.Net;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.Holds;
using AFH.Booking.Application.Models.AdviserProjection;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AFH.Booking.Tests;

public sealed class ReleaseHoldServiceTests
{
    private static readonly DateTime FixedNow = new(2026, 06, 04, 10, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IBookingHoldRepository> _holds = new();
    private readonly Mock<ICalendarGateway> _calendar = new();
    private readonly Mock<IAdviserProfileProjectionRepository> _profiles = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Mock<IBookingLifecycleRecorder> _lifecycle = new();
    private readonly List<BookingLifecycleEventRecord> _events = [];
    private readonly List<BookingLifecycleStepRecord> _steps = [];
    private readonly ReleaseHoldService _sut;

    public ReleaseHoldServiceTests()
    {
        _clock.Setup(x => x.UtcNow).Returns(FixedNow);
        _profiles.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string adviserId, CancellationToken _) => new AdviserProfileProjectionRecord
            {
                AdviserId = adviserId,
                MailboxUserId = $"{adviserId}@calendar.test"
            });
        _lifecycle.Setup(x => x.RecordEventAsync(It.IsAny<BookingLifecycleEventRecord>(), It.IsAny<CancellationToken>()))
            .Callback<BookingLifecycleEventRecord, CancellationToken>((entry, _) => _events.Add(entry))
            .ReturnsAsync("event-1");
        _lifecycle.Setup(x => x.RecordStepAsync("event-1", It.IsAny<BookingLifecycleStepRecord>(), It.IsAny<CancellationToken>()))
            .Callback<string, BookingLifecycleStepRecord, CancellationToken>((_, step, _) => _steps.Add(step))
            .Returns(Task.CompletedTask);

        _sut = new ReleaseHoldService(
            _holds.Object,
            _calendar.Object,
            _profiles.Object,
            _uow.Object,
            _clock.Object,
            _lifecycle.Object,
            NullLogger<ReleaseHoldService>.Instance);
    }

    [Fact]
    public async Task HandleAsync_ManualRelease_RecordsHoldReleasedEventWithManualActor()
    {
        var actor = BookingActorContext.InternalAdmin("admin-1", "Admin One", "corr-1");
        var hold = Hold(providerEventId: "event-1");
        _holds.Setup(x => x.GetForUpdateAsync("hold-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(hold);

        var result = await _sut.HandleAsync(new ReleaseHoldCommand
        {
            HoldId = "hold-1",
            ReasonCode = "ManualRelease",
            ReasonDetail = "Released by operator.",
            ReleaseKind = ReleaseHoldKind.ManualRelease,
            ActorContext = actor
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("hold-1", result.Value!.BookingId);
        Assert.Equal(BookingHoldStatus.Released, hold.Status);
        Assert.Equal("Released by operator.", hold.CancelReason);

        var recorded = Assert.Single(_events);
        Assert.Equal(LifecycleEventTypes.HoldReleased, recorded.EventType);
        Assert.Equal("ManualRelease", recorded.ReasonCode);
        Assert.Equal("Released by operator.", recorded.ReasonNotes);
        Assert.Null(recorded.NewState);
        Assert.Same(actor, recorded.ActorContext);
    }

    [Fact]
    public async Task HandleAsync_Expiry_RecordsHoldExpiredEventWithSystemActor()
    {
        var actor = BookingActorContext.SystemJob("HoldsCleanup", "corr-cleanup");
        var hold = Hold(expiresUtc: FixedNow.AddMinutes(-1));
        _holds.Setup(x => x.GetForUpdateAsync("hold-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(hold);

        var result = await _sut.HandleAsync(new ReleaseHoldCommand
        {
            HoldId = "hold-1",
            ReasonCode = "HoldExpired",
            ReasonDetail = "Expired by holds cleanup job.",
            ReleaseKind = ReleaseHoldKind.Expiry,
            ActorContext = actor
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingHoldStatus.Expired, hold.Status);

        var recorded = Assert.Single(_events);
        Assert.Equal(LifecycleEventTypes.HoldExpired, recorded.EventType);
        Assert.Equal("HoldExpired", recorded.ReasonCode);
        Assert.Null(recorded.NewState);
        Assert.Same(actor, recorded.ActorContext);
    }

    [Fact]
    public async Task HandleAsync_CalendarProviderEventExists_RecordsSucceededOutlookStep()
    {
        var hold = Hold(providerEventId: "event-1");
        _holds.Setup(x => x.GetForUpdateAsync("hold-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(hold);

        await _sut.HandleAsync(Command(), CancellationToken.None);

        var outlook = Assert.Single(_steps, x => x.StepName == LifecycleStepNames.Outlook);
        Assert.Equal(LifecycleStepStatuses.Succeeded, outlook.Status);
        Assert.Null(outlook.ErrorCode);
        _calendar.Verify(x => x.CancelBookingEventAsync("user-1@calendar.test", "event-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NoCalendarProviderEvent_RecordsSkippedOutlookStep()
    {
        var hold = Hold(providerEventId: null);
        _holds.Setup(x => x.GetForUpdateAsync("hold-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(hold);

        await _sut.HandleAsync(Command(), CancellationToken.None);

        var outlook = Assert.Single(_steps, x => x.StepName == LifecycleStepNames.Outlook);
        Assert.Equal(LifecycleStepStatuses.Skipped, outlook.Status);
        _calendar.Verify(x => x.CancelBookingEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CalendarCancelFails_RecordsFailedOutlookStepAndStillReleasesHold()
    {
        var hold = Hold(providerEventId: "event-1");
        _holds.Setup(x => x.GetForUpdateAsync("hold-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(hold);
        _calendar.Setup(x => x.CancelBookingEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("calendar unavailable"));

        var result = await _sut.HandleAsync(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingHoldStatus.Released, hold.Status);
        var outlook = Assert.Single(_steps, x => x.StepName == LifecycleStepNames.Outlook);
        Assert.Equal(LifecycleStepStatuses.Failed, outlook.Status);
        Assert.Equal(LifecycleErrorCodes.CalendarCancelFailed, outlook.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ConfirmedHoldCannotBeReleased()
    {
        var hold = Hold(status: BookingHoldStatus.Confirmed);
        _holds.Setup(x => x.GetForUpdateAsync("hold-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(hold);

        var result = await _sut.HandleAsync(Command(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        _lifecycle.Verify(x => x.RecordEventAsync(It.IsAny<BookingLifecycleEventRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _holds.Verify(x => x.UpdateAsync(It.IsAny<BookingHold>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(BookingHoldStatus.Released)]
    [InlineData(BookingHoldStatus.Expired)]
    [InlineData(BookingHoldStatus.Cancelled)]
    public async Task HandleAsync_DuplicateReleaseOrExpiry_DoesNotRecordAnotherLifecycleEvent(BookingHoldStatus status)
    {
        var hold = Hold(status: status);
        _holds.Setup(x => x.GetForUpdateAsync("hold-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(hold);

        var result = await _sut.HandleAsync(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("hold-1", result.Value!.BookingId);
        Assert.Empty(_events);
        Assert.Empty(_steps);
        _calendar.Verify(x => x.CancelBookingEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _holds.Verify(x => x.UpdateAsync(It.IsAny<BookingHold>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_StringWrapper_UsesCommandImplementation()
    {
        var hold = Hold(providerEventId: null);
        _holds.Setup(x => x.GetForUpdateAsync("hold-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(hold);

        var result = await _sut.HandleAsync("hold-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("hold-1", result.Value!.BookingId);
        Assert.Equal(BookingHoldStatus.Released, hold.Status);
        var recorded = Assert.Single(_events);
        Assert.Equal(LifecycleEventTypes.HoldReleased, recorded.EventType);
        Assert.Equal(BookingActorContext.SourceInternalAdmin, recorded.ActorContext?.SourceApplication);
    }

    [Fact]
    public async Task HandleAsync_Success_ResponseShapeRemainsBookingIdOnly()
    {
        var hold = Hold(providerEventId: null);
        _holds.Setup(x => x.GetForUpdateAsync("hold-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(hold);

        var result = await _sut.HandleAsync(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("hold-1", result.Value!.BookingId);
        Assert.Null(result.Value.Success);
        Assert.Null(result.Value.Error);
    }

    private static ReleaseHoldCommand Command()
        => new()
        {
            HoldId = "hold-1",
            ReasonCode = "ManualRelease",
            ReasonDetail = "Released by operator.",
            ReleaseKind = ReleaseHoldKind.ManualRelease,
            ActorContext = BookingActorContext.InternalAdmin("admin-1", "Admin One", "corr-1")
        };

    private static BookingHold Hold(
        BookingHoldStatus status = BookingHoldStatus.Active,
        string? providerEventId = null,
        DateTime? expiresUtc = null)
        => BookingHold.Rehydrate(
            "hold-1",
            "slot-1",
            "user-1",
            status,
            FixedNow.AddMinutes(-10),
            expiresUtc ?? FixedNow.AddMinutes(10),
            status == BookingHoldStatus.Confirmed ? FixedNow.AddMinutes(-5) : null,
            status == BookingHoldStatus.Released ? FixedNow.AddMinutes(-5) : null,
            status == BookingHoldStatus.Cancelled ? FixedNow.AddMinutes(-5) : null,
            null,
            providerEventId,
            null);
}
