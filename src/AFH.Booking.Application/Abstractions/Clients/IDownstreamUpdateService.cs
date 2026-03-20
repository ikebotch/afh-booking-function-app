using AFH.Booking.Contracts.V1.Responses;

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
