namespace AFH.Notification.Application.Models;

public sealed record NotificationSettingItem(
    string Key,
    string Category,
    string Value,
    bool IsSecret,
    string? Description,
    DateTime UpdatedUtc,
    string? UpdatedBy);

public sealed record NotificationSettingUpsert(
    string Key,
    string Category,
    string Value,
    bool IsSecret,
    string? Description,
    string? Actor);
