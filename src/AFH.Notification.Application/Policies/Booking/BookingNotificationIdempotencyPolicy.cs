using AFH.Notification.Application.Abstractions;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Application.Policies.Booking;

public sealed class BookingNotificationIdempotencyPolicy : INotificationIdempotencyPolicy
{
    public bool CanHandle(NotificationRequested request)
        => string.Equals(request.SourceSystem, "Booking", StringComparison.OrdinalIgnoreCase);

    public string GetPrimaryId(NotificationRequested request)
    {
        if (request.Data.TryGetValue("IdempotencyKey", out var idempotencyKey) && !string.IsNullOrWhiteSpace(idempotencyKey))
            return idempotencyKey;

        if (request.Data.TryGetValue("BookingId", out var bookingId) && !string.IsNullOrWhiteSpace(bookingId))
            return bookingId;

        if (request.Data.TryGetValue("HoldId", out var holdId) && !string.IsNullOrWhiteSpace(holdId))
            return holdId;

        if (request.Data.TryGetValue("TransactionId", out var txId) && !string.IsNullOrWhiteSpace(txId))
            return txId;

        return request.CorrelationId;
    }
}
