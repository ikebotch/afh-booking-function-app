using System.ComponentModel.DataAnnotations;
namespace AFH.Booking.Contracts.Requests;

public sealed record CancelBookingRequest(
    [property: Required] string BookingId,
    string? Reason = null
);
