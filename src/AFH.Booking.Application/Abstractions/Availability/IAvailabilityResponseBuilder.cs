using AFH.Booking.Application.Availability;
using AFH.Booking.Application.Models.Availability;
using AFH.Booking.Domain.Availability;
using AFH.Booking.Domain.Common;

namespace AFH.Booking.Application.Abstractions.Availability;

public interface IAvailabilityResponseBuilder
{
    Result<GetAvailabilityResponse> Success(
        GetAvailabilityQuery query,
        string transactionId,
        IReadOnlyList<AvailabilitySlotResult> slots,
        string? nextCursor);

    Result<GetAvailabilityResponse> Empty(string? nextCursor);
}
