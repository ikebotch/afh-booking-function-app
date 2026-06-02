using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Bookings;
using AFH.Booking.Domain.Availability;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using Moq;
using System.Net;

namespace AFH.Booking.Tests;

public sealed class SelfServiceJourneyApplicationTests
{
    [Fact]
    public async Task RearrangementOptions_ResponseIncludesTopLevelTransactionIdAndNestedCompatibilityValues()
    {
        var hold = BookingHold.Rehydrate(
            "booking-1",
            "slot-1",
            "user-1",
            BookingHoldStatus.Confirmed,
            DateTime.UtcNow.AddHours(-2),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(-1),
            null,
            null,
            null,
            "provider-1",
            null);

        var slot = BookingSlot.Rehydrate(
            "slot-1",
            "tx-1",
            "adviser-1",
            "Adviser One",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(1),
            5,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            DateTime.UtcNow);

        var tx = BookingTransaction.Rehydrate(
            "tx-1",
            "txn-ref",
            DateTime.UtcNow,
            TimeSpan.FromHours(1),
            "Europe/London",
            true,
            "Review",
            null,
            BookingTransactionStatus.Completed,
            DateTime.UtcNow,
            null);

        var holds = new Mock<IBookingHoldRepository>();
        holds.Setup(x => x.GetAsync("booking-1", It.IsAny<CancellationToken>())).ReturnsAsync(hold);

        var slots = new Mock<IBookingSlotRepository>();
        slots.Setup(x => x.GetAsync("slot-1", It.IsAny<CancellationToken>())).ReturnsAsync(slot);

        var transactions = new Mock<IBookingTransactionRepository>();
        transactions.Setup(x => x.GetAsync("tx-1", It.IsAny<CancellationToken>())).ReturnsAsync(tx);

        var availability = new Mock<IAvailabilityService>();
        availability.Setup(x => x.HandleAsync(It.IsAny<GetAvailabilityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetAvailabilityQuery query, CancellationToken _) => Result<GetAvailabilityResponse>.Ok(new GetAvailabilityResponse
            {
                TransactionId = query.TransactionId,
                Advisers = [],
                Paging = new()
            }));

        var service = new RearrangementOptionsService(
            holds.Object,
            slots.Object,
            transactions.Object,
            availability.Object);

        var result = await service.HandleAsync(
            new GetRearrangementOptionsCommand
            {
                BookingId = "booking-1",
                Limit = 5
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("tx-1", result.Value!.TransactionId);
        Assert.Equal("tx-1", result.Value.AssignedAdviserOptions.TransactionId);
        Assert.Equal("tx-1", result.Value.AlternativeAdviserOptions.TransactionId);
    }

    [Fact]
    public async Task RearrangementOptions_CancelledBooking_ReturnsConflictBeforeAvailabilitySearch()
    {
        var hold = BookingHold.Rehydrate(
            "booking-1",
            "slot-1",
            "user-1",
            BookingHoldStatus.Cancelled,
            DateTime.UtcNow.AddHours(-2),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(-1),
            null,
            DateTime.UtcNow.AddMinutes(-10),
            "Client request",
            "provider-1",
            null);

        var holds = new Mock<IBookingHoldRepository>();
        holds.Setup(x => x.GetAsync("booking-1", It.IsAny<CancellationToken>())).ReturnsAsync(hold);
        var availability = new Mock<IAvailabilityService>();

        var service = new RearrangementOptionsService(
            holds.Object,
            Mock.Of<IBookingSlotRepository>(),
            Mock.Of<IBookingTransactionRepository>(),
            availability.Object);

        var result = await service.HandleAsync(
            new GetRearrangementOptionsCommand
            {
                BookingId = "booking-1",
                Limit = 5
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        availability.Verify(x => x.HandleAsync(It.IsAny<GetAvailabilityQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
