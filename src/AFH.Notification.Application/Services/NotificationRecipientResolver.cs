using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Application.Services;

public sealed class NotificationRecipientResolver : INotificationRecipientResolver
{
    private readonly IReadOnlyList<INotificationRoutingPolicy> _policies;

    public NotificationRecipientResolver(IEnumerable<INotificationRoutingPolicy> policies)
    {
        _policies = policies.ToArray();
    }

    public Task<NotificationRoute> ResolveAsync(NotificationRequested notification, CancellationToken ct)
    {
        var policy = _policies.FirstOrDefault(policy => policy.CanHandle(notification));
        if (policy is null)
            return Task.FromResult(new NotificationRoute(notification.Recipients.Select(NormaliseChannels).Where(HasAnyDeliveryTarget).ToArray(), false));

        return Task.FromResult(policy.Resolve(notification));
    }

    private static NotificationRecipient NormaliseChannels(NotificationRecipient recipient)
    {
        var channels = recipient.PreferredChannels is { Count: > 0 }
            ? recipient.PreferredChannels.Where(channel => channel != NotificationChannel.Unknown).Distinct().ToArray()
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
}
