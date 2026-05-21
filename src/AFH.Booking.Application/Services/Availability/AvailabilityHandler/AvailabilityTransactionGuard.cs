using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Availability;
using AFH.Booking.Domain.Common;

namespace AFH.Booking.Application.Availability;

public sealed class AvailabilityTransactionGuard : IAvailabilityTransactionGuard
{
    private readonly IBookingTransactionRepository _txRepo;

    public AvailabilityTransactionGuard(IBookingTransactionRepository txRepo)
    {
        _txRepo = txRepo;
    }

    public async Task<Result<GetAvailabilityResponse>?> EnsureOpenAsync(GetAvailabilityQuery query, CancellationToken ct)
    {
        var transactionRef = query.TransactionId ?? query.ClientId;
        if (string.IsNullOrWhiteSpace(transactionRef))
            return null;

        var existing = await _txRepo.GetLatestByTransactionRefAsync(transactionRef.Trim(), ct);
        if (existing is null || existing.Status == BookingTransactionStatus.Open)
            return null;

        return Result<GetAvailabilityResponse>.Fail(
            HttpStatusCode.Conflict,
            $"Transaction reference '{transactionRef.Trim()}' is already {existing.Status}.",
            "TransactionClosed");
    }
}
