using System.ComponentModel.DataAnnotations;

namespace AFH.Booking.Contracts.Requests;

public sealed record ConfirmBookingRequest(
    [property: Required] string BookingId,
    string? TransactionId = null
);
