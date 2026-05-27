using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Application.Services;

public sealed class NotificationRecipientResolver : INotificationRecipientResolver
{
    public Task<NotificationRoute> ResolveAsync(NotificationRequested notification, CancellationToken ct)
    {
        var eligibleRecipientTypes = GetEligibleRecipientTypes(notification.Actor.Type);
        var recipients = notification.Recipients
            .Where(recipient => recipient.Type != NotificationRecipientType.Unknown)
            .Where(recipient => eligibleRecipientTypes.Contains(recipient.Type))
            .Select(NormaliseChannels)
            .Where(HasAnyDeliveryTarget)
            .DistinctBy(GetRecipientKey)
            .ToArray();

        var copyContactCentre = ShouldCopyContactCentre(notification, recipients);
        return Task.FromResult(new NotificationRoute(recipients, copyContactCentre));
    }

    private static HashSet<NotificationRecipientType> GetEligibleRecipientTypes(NotificationActorType actorType)
        => actorType switch
        {
            NotificationActorType.Client =>
            [
                NotificationRecipientType.Client,
                NotificationRecipientType.Adviser,
                NotificationRecipientType.ContactCentre
            ],
            NotificationActorType.Adviser =>
            [
                NotificationRecipientType.Client,
                NotificationRecipientType.Adviser,
                NotificationRecipientType.ContactCentre
            ],
            NotificationActorType.Admin or NotificationActorType.System =>
            [
                NotificationRecipientType.Client,
                NotificationRecipientType.Adviser,
                NotificationRecipientType.ContactCentre,
                NotificationRecipientType.Internal
            ],
            _ =>
            [
                NotificationRecipientType.Client,
                NotificationRecipientType.Adviser,
                NotificationRecipientType.ContactCentre,
                NotificationRecipientType.Internal
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
            recipient.Type,
            recipient.Email?.Trim().ToUpperInvariant(),
            recipient.MobileNumber?.Trim(),
            recipient.PushTarget?.Trim());

    private static bool ShouldCopyContactCentre(
        NotificationRequested notification,
        IReadOnlyCollection<NotificationRecipient> recipients)
    {
        if (!notification.SourceSystem.Equals("Booking", StringComparison.OrdinalIgnoreCase))
            return false;

        if (notification.Actor.Type is NotificationActorType.Admin or NotificationActorType.System)
            return true;

        return recipients.Any(recipient => recipient.Type == NotificationRecipientType.ContactCentre);
    }
}
