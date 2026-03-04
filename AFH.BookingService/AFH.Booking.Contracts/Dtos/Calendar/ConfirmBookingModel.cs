using AFH.Booking.Contracts.Requests;

namespace AFH.Booking.Application.Bookings.Commands;

public sealed class ConfirmBookingModel
{
    public string BookingId { get; }
    public ConfirmBookingRequest Request { get; }
    public ConfirmBookingModel(string bookingId, ConfirmBookingRequest request)
    {
        BookingId = bookingId;
        Request = request;
    }
}
