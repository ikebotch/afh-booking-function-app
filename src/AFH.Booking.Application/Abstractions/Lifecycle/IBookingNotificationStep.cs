using AFH.Booking.Application.Models.Notifications;

namespace AFH.Booking.Application.Abstractions.Lifecycle;

public interface IBookingNotificationStep
{
    Task<(string Status, string? ErrorCode, string? ErrorDetails)> ExecuteAsync(
        string lifecycleEventType,
        string correlationId,
        string actorType,
        IReadOnlyList<BookingNotificationRecipient> recipients,
        IReadOnlyDictionary<string, string> data,
        CancellationToken ct);
}
