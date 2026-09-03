using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Bookings;

public static class DownstreamPerformedByResolver
{
    public static string Resolve(BookingActorContext? actor, string? fallbackActorType = null)
    {
        var actorType = string.IsNullOrWhiteSpace(actor?.ActorType)
            ? fallbackActorType?.Trim()
            : actor.ActorType.Trim();

        if (actor?.IsSelfService == true ||
            string.Equals(actorType, BookingActorContext.ActorClient, StringComparison.OrdinalIgnoreCase))
        {
            return "Self";
        }

        if (string.Equals(actorType, BookingActorContext.ActorPartner, StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(actor?.PartnerName)
                ? "Partner"
                : $"Partner:{actor.PartnerName.Trim()}";
        }

        if (string.Equals(actorType, BookingActorContext.ActorInternalAdmin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(actorType, BookingActorContext.ActorAdviser, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(actorType, BookingActorContext.ActorManager, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(actorType, BookingActorContext.ActorSystem, StringComparison.OrdinalIgnoreCase))
        {
            return "AFH";
        }

        return string.IsNullOrWhiteSpace(actorType) ? "Unknown" : actorType;
    }
}
