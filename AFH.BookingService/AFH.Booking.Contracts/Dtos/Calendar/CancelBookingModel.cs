using AFH.Booking.Contracts.Requests;

namespace AFH.Booking.Application.Bookings.Commands;

public sealed class CancelBookingModel
{
    public string BookingId { get; set; }
    public CancelBookingRequest Request { get; set; }

    public CancelBookingModel(string bookingId, CancelBookingRequest request)
    {
        BookingId = bookingId;
        Request = request;
    }
}
