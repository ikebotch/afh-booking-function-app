using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Bookings;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Options;
using Moq;

namespace AFH.Booking.Tests;

public sealed class BookingDetailsServiceTests
{
    [Fact]
    public async Task HandleAsync_IncludesSecureSelfServiceLinks()
    {
        var startUtc = new DateTime(2026, 07, 15, 9, 0, 0, DateTimeKind.Utc);
        var hold = BookingHold.Rehydrate(
            "booking-1",
            "slot-1",
            "client-1",
            BookingHoldStatus.Confirmed,
            startUtc.AddDays(-1),
            startUtc.AddHours(1),
            startUtc.AddDays(-1).AddMinutes(5),
            null,
            null,
            null,
            "provider-1",
            null);
        var slot = BookingSlot.Rehydrate(
            id: "slot-1",
            transactionRef: "tx-1",
            adviserId: "adv-1",
            adviserName: "Adviser One",
            startUtc: startUtc,
            endUtc: startUtc.AddHours(1),
            score: 5,
            scoreBreakdown: null,
            locationRef: null,
            travelMinutes: 0,
            companyBufferMinutes: 0,
            distanceMiles: null,
            travelStatus: null,
            travelMessage: null,
            createdUtc: startUtc.AddDays(-2));
        var transaction = BookingTransaction.Rehydrate(
            id: "tx-1",
            transactionRef: "TRX-1",
            proposedStartUtc: startUtc,
            duration: TimeSpan.FromHours(1),
            timezone: "Europe/London",
            isRemote: true,
            meetingType: "Review",
            locationRef: null,
            status: BookingTransactionStatus.Completed,
            createdUtc: startUtc.AddDays(-2),
            expiresUtc: startUtc.AddHours(2));

        var holds = new Mock<IBookingHoldRepository>();
        holds.Setup(x => x.GetAsync("booking-1", It.IsAny<CancellationToken>())).ReturnsAsync(hold);
        var slots = new Mock<IBookingSlotRepository>();
        slots.Setup(x => x.GetAsync("slot-1", It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        var transactions = new Mock<IBookingTransactionRepository>();
        transactions.Setup(x => x.GetAsync("tx-1", It.IsAny<CancellationToken>())).ReturnsAsync(transaction);
        var tokenService = new Mock<IBookingTokenService>();
        tokenService
            .Setup(x => x.GenerateClientAccessTokenAsync("booking-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Ok("opaque+/= token"));

        var service = new BookingDetailsService(
            holds.Object,
            slots.Object,
            transactions.Object,
            tokenService.Object,
            Options.Create(new NotificationsOptions { ClientPortalBaseUrl = "https://client.example/app/" }));

        var result = await service.HandleAsync(new GetBookingDetailsQuery { BookingId = "booking-1" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("https://client.example/app/bookings/booking-1?token=opaque%2B%2F%3D%20token", result.Value!.ViewBookingUrl);
        Assert.Equal("https://client.example/app/bookings/booking-1/cancel?token=opaque%2B%2F%3D%20token", result.Value.CancelBookingUrl);
        Assert.Equal("https://client.example/app/bookings/booking-1/reschedule?token=opaque%2B%2F%3D%20token", result.Value.RescheduleBookingUrl);
    }
}
