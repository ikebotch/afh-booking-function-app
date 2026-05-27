using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
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
        var policy = _policies.FirstOrDefault(policy => policy.CanHandle(notification))
            ?? throw new NotSupportedException($"Notification routing policy for source system '{notification.SourceSystem}' is not registered.");

        return Task.FromResult(policy.Resolve(notification));
    }
}
