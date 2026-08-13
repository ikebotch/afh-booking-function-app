using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Application.Policies.Booking;

public sealed class BookingNotificationRoutingPolicy : INotificationRoutingPolicy
{
    public bool CanHandle(NotificationRequested notification)
        => string.Equals(notification.SourceSystem, "Booking", StringComparison.OrdinalIgnoreCase);

    public NotificationRoute Resolve(NotificationRequested notification)
    {
        var eligibleRecipientTypes = GetEligibleRecipientTypes(notification.Actor.ActorType);
        var recipients = notification.Recipients
            .Where(recipient => !string.IsNullOrWhiteSpace(recipient.RecipientType))
            .Where(recipient => eligibleRecipientTypes.Contains(recipient.RecipientType.Trim()))
            .Select(NormaliseChannels)
            .Where(HasAnyDeliveryTarget)
            .DistinctBy(GetRecipientKey)
            .ToArray();

        var copyContactCentre = ShouldCopyContactCentre(notification, notification.Recipients);
        return new NotificationRoute(recipients, copyContactCentre);
    }

    private static HashSet<string> GetEligibleRecipientTypes(string? actorType)
        => actorType?.Trim() switch
        {
            var value when IsActor(value, "Client") =>
            [
                "Client",
                "Adviser",
                "Manager",
                "ContactCentre"
            ],
            var value when IsActor(value, "Adviser") =>
            [
                "Client",
                "Adviser",
                "Manager",
                "ContactCentre"
            ],
            var value when IsActor(value, "Admin") || IsActor(value, "System") =>
            [
                "Client",
                "Adviser",
                "Manager",
                "ContactCentre",
                "Internal"
            ],
            _ =>
            [
                "Client",
                "Adviser",
                "Manager",
                "ContactCentre",
                "Internal"
            ]
        };

    private static NotificationRecipient NormaliseChannels(NotificationRecipient recipient)
    {
        var channels = recipient.PreferredChannels is { Count: > 0 }
            ? recipient.PreferredChannels
                .Where(channel => channel != NotificationChannel.Unknown)
                .Distinct()
                .ToArray()
            : InferChannels(recipient);

        return recipient with { PreferredChannels = channels };
    }

    private static NotificationChannel[] InferChannels(NotificationRecipient recipient)
    {
        var channels = new List<NotificationChannel>(capacity: 3);

        if (!string.IsNullOrWhiteSpace(recipient.Email))
            channels.Add(NotificationChannel.Email);

        if (!string.IsNullOrWhiteSpace(recipient.MobileNumber))
            channels.Add(NotificationChannel.Sms);

        if (!string.IsNullOrWhiteSpace(recipient.PushTarget))
            channels.Add(NotificationChannel.Push);

        return [.. channels];
    }

    private static bool HasAnyDeliveryTarget(NotificationRecipient recipient)
        => recipient.PreferredChannels is { Count: > 0 };

    private static string GetRecipientKey(NotificationRecipient recipient)
        => string.Join(
            "|",
            recipient.RecipientType.Trim(),
            recipient.Email?.Trim().ToUpperInvariant(),
            recipient.MobileNumber?.Trim(),
            recipient.PushTarget?.Trim());

    private static bool ShouldCopyContactCentre(
        NotificationRequested notification,
        IReadOnlyCollection<NotificationRecipient> recipients)
    {
        if (IsActor(notification.Actor.ActorType, "Admin") || IsActor(notification.Actor.ActorType, "System"))
            return true;

        return recipients.Any(recipient => IsRecipientType(recipient, "ContactCentre"));
    }

    private static bool IsActor(string? actual, string expected)
        => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsRecipientType(NotificationRecipient recipient, string expected)
        => string.Equals(recipient.RecipientType, expected, StringComparison.OrdinalIgnoreCase);
}
