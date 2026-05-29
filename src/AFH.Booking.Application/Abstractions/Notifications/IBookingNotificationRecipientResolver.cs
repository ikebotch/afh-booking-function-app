using AFH.Booking.Application.Models.Notifications;

namespace AFH.Booking.Application.Abstractions.Notifications;

public interface IBookingNotificationRecipientResolver
{
    Task<IReadOnlyList<BookingNotificationRecipient>> ResolveAsync(
        BookingNotificationPolicy policy,
        IReadOnlyList<BookingNotificationRecipient> requestedRecipients,
        IReadOnlyDictionary<string, string> data,
        CancellationToken ct);
}
