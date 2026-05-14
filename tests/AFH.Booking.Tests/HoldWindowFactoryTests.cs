using AFH.Booking.Application.Holds;
using AFH.Booking.Domain.Bookings;

namespace AFH.Booking.Tests;

public sealed class HoldWindowFactoryTests
{
    [Fact]
    public void Create_ForInPersonBooking_UsesPersistedSlotTravelAndCompanyBuffer()
    {
        var startUtc = new DateTime(2026, 05, 21, 10, 0, 0, DateTimeKind.Utc);
        var slot = CreateSlot(startUtc, startUtc.AddMinutes(30), travelMinutes: 5, companyBufferMinutes: 30);
        var transaction = CreateTransaction(startUtc, isRemote: false);

        var windows = new HoldWindowFactory().Create(slot, transaction);

        Assert.Equal(startUtc.AddMinutes(-35), windows.HoldStartUtc);
        Assert.Equal(startUtc.AddMinutes(60), windows.HoldEndUtc);
        Assert.Equal(5, windows.TravelMinutes);
        Assert.Equal(30, windows.CompanyBufferMinutes);
        Assert.True(windows.HasBuffer);
    }

    [Fact]
    public void Create_ForRemoteBooking_ZeroesPersistedTravelAndBuffer()
    {
        var startUtc = new DateTime(2026, 05, 21, 10, 0, 0, DateTimeKind.Utc);
        var slot = CreateSlot(startUtc, startUtc.AddMinutes(30), travelMinutes: 25, companyBufferMinutes: 30);
        var transaction = CreateTransaction(startUtc, isRemote: true);

        var windows = new HoldWindowFactory().Create(slot, transaction);

        Assert.Equal(startUtc, windows.HoldStartUtc);
        Assert.Equal(startUtc.AddMinutes(30), windows.HoldEndUtc);
        Assert.Equal(0, windows.TravelMinutes);
        Assert.Equal(0, windows.CompanyBufferMinutes);
        Assert.False(windows.HasBuffer);
    }

    private static BookingTransaction CreateTransaction(DateTime startUtc, bool isRemote) =>
        BookingTransaction.Rehydrate(
            id: "tx-1",
            transactionRef: "TRX-1",
            proposedStartUtc: startUtc,
            duration: TimeSpan.FromMinutes(30),
            timezone: "Europe/London",
            isRemote: isRemote,
            meetingType: "Review",
            locationRef: "loc-1",
            status: BookingTransactionStatus.Open,
            createdUtc: startUtc.AddHours(-1),
            expiresUtc: startUtc.AddHours(1));

    private static BookingSlot CreateSlot(DateTime startUtc, DateTime endUtc, int travelMinutes, int companyBufferMinutes) =>
        BookingSlot.Rehydrate(
            id: "slot-1",
            transactionRef: "tx-1",
            adviserId: "adv-1",
            adviserName: "Adviser One",
            startUtc: startUtc,
            endUtc: endUtc,
            score: 5,
            scoreBreakdown: null,
            locationRef: "loc-1",
            travelMinutes: travelMinutes,
            companyBufferMinutes: companyBufferMinutes,
            distanceMiles: 12,
            travelStatus: "Eligible",
            travelMessage: null,
            createdUtc: startUtc.AddHours(-1));
}
