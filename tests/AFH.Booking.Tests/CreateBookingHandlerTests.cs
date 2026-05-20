using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AFH.Booking.Application.Abstractions.Bookings.Holds;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.Holds;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using Moq;
using Xunit;

namespace AFH.Booking.Tests;

public class CreateBookingHandlerTests
{
    private readonly Mock<IBookingContextLoader> _loader;
    private readonly Mock<IBookingHoldService> _holdService;
    private readonly Mock<IBookingCalendarService> _calendarService;
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IClock> _clock;
    private readonly CreateBookingHandler _sut;

    private static readonly DateTime FixedNow = new DateTime(2026, 03, 25, 10, 0, 0, DateTimeKind.Utc);

    public CreateBookingHandlerTests()
    {
        _loader = new Mock<IBookingContextLoader>();
        _holdService = new Mock<IBookingHoldService>();
        _calendarService = new Mock<IBookingCalendarService>();
        _uow = new Mock<IUnitOfWork>();
        _clock = new Mock<IClock>();

        _clock.Setup(c => c.UtcNow).Returns(FixedNow);

        _sut = new CreateBookingHandler(
            _loader.Object,
            _holdService.Object,
            _calendarService.Object,
            _uow.Object,
            _clock.Object);
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
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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
