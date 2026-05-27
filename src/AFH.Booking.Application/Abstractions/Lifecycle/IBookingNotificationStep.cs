using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Booking.Application.Abstractions.Lifecycle;

public interface IBookingNotificationStep
{
    Task<(string Status, string? ErrorCode, string? ErrorDetails)> ExecuteAsync(
        string lifecycleEventType,
        string correlationId,
        string actorType,
        IReadOnlyList<NotificationRecipient> recipients,
        IReadOnlyDictionary<string, string> data,
        CancellationToken ct);
}
