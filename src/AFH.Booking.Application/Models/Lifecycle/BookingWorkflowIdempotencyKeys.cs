namespace AFH.Booking.Application.Models.Lifecycle;

public static class BookingWorkflowIdempotencyKeys
{
    public static string Confirmation(string holdId)
        => $"confirm:{Normalize(holdId)}";

    public static string Cancellation(string bookingId, string? actorType, string? reasonCode)
        => $"cancel:{Normalize(bookingId)}:{Normalize(actorType)}:{Normalize(reasonCode)}";

    public static string Rearrangement(string oldBookingId, string newSlotId, string? actorType)
        => $"rearrange:{Normalize(oldBookingId)}:{Normalize(newSlotId)}:{Normalize(actorType)}";

    public static string HoldRelease(string holdId, string releaseKind)
        => $"release:{Normalize(holdId)}:{Normalize(releaseKind)}";

    public static string NoShow(string bookingId, string? actorType)
        => $"noshow:{Normalize(bookingId)}:{Normalize(actorType)}";

    public static string Notification(string notificationType, string bookingId)
        => $"{Normalize(notificationType)}:{Normalize(bookingId)}";

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? "none"
            : value.Trim().ToLowerInvariant();
}
