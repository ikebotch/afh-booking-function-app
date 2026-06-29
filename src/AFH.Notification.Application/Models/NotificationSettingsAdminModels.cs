namespace AFH.Notification.Application.Models;

public sealed record NotificationChannelSettingItem(
    string Id,
    string Channel,
    string Provider,
    string SenderId,
    string Format,
    string Status,
    string? Description);

public sealed record NotificationChannelSettingUpsert(
    string? Id,
    string? Channel,
    string? Provider,
    string? SenderId,
    string? Format,
    string? Status,
    string? Description,
    string? Actor);

public sealed record NotificationLifecycleEventItem(
    string Id,
    string Event,
    string Owner,
    string Description,
    string Status,
    IReadOnlyList<string> Variables);

public sealed record NotificationRetryPolicyItem(
    string Id,
    string EventType,
    string Channel,
    int MaxRetries,
    int DelayMin,
    string Strategy,
    string Status,
    string? Description);

public sealed record NotificationRetryPolicyUpsert(
    string? Id,
    string? EventType,
    string? Channel,
    int? MaxRetries,
    int? DelayMin,
    string? Strategy,
    string? Status,
    string? Description,
    string? Actor);
