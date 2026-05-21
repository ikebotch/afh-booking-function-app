using AFH.Booking.Application.EmailTemplates;
using AFH.Booking.Domain.Bookings;

namespace AFH.Booking.Application.Holds;

public sealed class HoldWindowFactory : IHoldWindowFactory
{
    private const int DefaultCompanyBufferMinutes = 30;

    public HoldWindows Create(
        BookingSlot slot,
        BookingTransaction transaction)
    {
        var travelMinutes = transaction.IsRemote
            ? 0
            : Math.Max(0, slot.TravelMinutes ?? 0);

        var companyBufferMinutes = transaction.IsRemote
            ? 0
            : Math.Max(0, slot.CompanyBufferMinutes ?? DefaultCompanyBufferMinutes);

        var preMeetingMinutes = travelMinutes + companyBufferMinutes;
        var postMeetingMinutes = companyBufferMinutes;

        var start = slot.StartUtc.AddMinutes(-preMeetingMinutes);
        var end = slot.EndUtc.AddMinutes(postMeetingMinutes);

        if (end <= start)
        {
            return new HoldWindows(
                slot.StartUtc,
                slot.EndUtc,
                0,
                0,
                false);
        }

        return new HoldWindows(
            start,
            end,
            travelMinutes,
            companyBufferMinutes,
            preMeetingMinutes > 0 || postMeetingMinutes > 0);
    }
}