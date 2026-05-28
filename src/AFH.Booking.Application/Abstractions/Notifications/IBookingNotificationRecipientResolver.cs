using AFH.Booking.Application.Models.Notifications;
using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Booking.Application.Abstractions.Notifications;

public interface IBookingNotificationRecipientResolver
{
    Task<IReadOnlyList<NotificationRecipient>> ResolveAsync(
        BookingNotificationPolicy policy,
        IReadOnlyList<NotificationRecipient> requestedRecipients,
        IReadOnlyDictionary<string, string> data,
        CancellationToken ct);
}
