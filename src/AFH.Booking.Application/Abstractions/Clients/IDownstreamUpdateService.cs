using AFH.Booking.Application.Models.Clients;

namespace AFH.Booking.Application.Abstractions.Clients;

public interface IDownstreamUpdateService
{
    Task<DownstreamUpdateResponse> PublishBookingChangeAsync(
        string bookingId,
        string changeType,
        string transactionRef,
        string payloadJson,
        CancellationToken ct);
}
