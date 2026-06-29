using AFH.Notification.Application.Models;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationSettingsService
{
    Task<IReadOnlyList<NotificationSettingItem>> ListAsync(string? category, CancellationToken ct);
    Task<NotificationSettingItem?> GetAsync(string key, CancellationToken ct);
    Task<NotificationSettingItem> UpsertAsync(NotificationSettingUpsert setting, CancellationToken ct);
    Task<bool> DeleteAsync(string key, CancellationToken ct);
    Task<IReadOnlyList<NotificationChannelSettingItem>> ListChannelSettingsAsync(CancellationToken ct);
    Task<NotificationChannelSettingItem> UpsertChannelSettingAsync(NotificationChannelSettingUpsert setting, CancellationToken ct);
    Task<IReadOnlyList<NotificationLifecycleEventItem>> ListLifecycleEventsAsync(CancellationToken ct);
    Task<IReadOnlyList<NotificationRetryPolicyItem>> ListRetryPoliciesAsync(CancellationToken ct);
    Task<NotificationRetryPolicyItem> UpsertRetryPolicyAsync(NotificationRetryPolicyUpsert policy, CancellationToken ct);
}
