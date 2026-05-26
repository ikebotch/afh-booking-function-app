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
