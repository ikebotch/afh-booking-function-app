using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AFH.Notification.Infrastructure.Persistence;

public sealed class NotificationSettingsService : INotificationSettingsService
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private const string ChannelSettingsCategory = "ChannelSettings";
    private const string LifecycleEventsCategory = "LifecycleEvents";
    private const string RetryPoliciesCategory = "RetryPolicies";

    private readonly NotificationDbContext _db;

    public NotificationSettingsService(NotificationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<NotificationSettingItem>> ListAsync(string? category, CancellationToken ct)
    {
        var query = _db.NotificationSettings.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category == category.Trim());

        return await query
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Key)
            .Select(x => ToItem(x))
            .ToArrayAsync(ct);
    }

    public async Task<NotificationSettingItem?> GetAsync(string key, CancellationToken ct)
    {
        var normalizedKey = NormalizeKey(key);
        if (normalizedKey is null)
            return null;

        var item = await _db.NotificationSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == normalizedKey, ct);
        return item is null ? null : ToItem(item);
    }

    public async Task<NotificationSettingItem> UpsertAsync(NotificationSettingUpsert setting, CancellationToken ct)
    {
        var key = NormalizeKey(setting.Key) ?? throw new NotificationRequestValidationException("Setting key is required.");
        var category = string.IsNullOrWhiteSpace(setting.Category) ? "General" : setting.Category.Trim();
        var now = DateTime.UtcNow;

        var entity = await _db.NotificationSettings.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (entity is null)
        {
            entity = new NotificationSettingModel
            {
                Key = key,
                CreatedUtc = now
            };
            _db.NotificationSettings.Add(entity);
        }

        entity.Category = category;
        entity.Value = setting.Value ?? string.Empty;
        entity.IsSecret = setting.IsSecret;
        entity.Description = string.IsNullOrWhiteSpace(setting.Description) ? null : setting.Description.Trim();
        entity.UpdatedBy = string.IsNullOrWhiteSpace(setting.Actor) ? null : setting.Actor.Trim();
        entity.UpdatedUtc = now;

        await _db.SaveChangesAsync(ct);
        return ToItem(entity);
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken ct)
    {
        var normalizedKey = NormalizeKey(key);
        if (normalizedKey is null)
            return false;

        var entity = await _db.NotificationSettings.FirstOrDefaultAsync(x => x.Key == normalizedKey, ct);
        if (entity is null)
            return false;

        _db.NotificationSettings.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<NotificationChannelSettingItem>> ListChannelSettingsAsync(CancellationToken ct)
    {
        var items = await ListPayloadSettingsAsync<NotificationChannelSettingItem>(ChannelSettingsCategory, ct);
        return items.Length == 0 ? DefaultChannelSettings() : items;
    }

    public async Task<NotificationChannelSettingItem> UpsertChannelSettingAsync(NotificationChannelSettingUpsert setting, CancellationToken ct)
    {
        var channel = Required(setting.Channel, "Channel is required.");
        var id = Slug(setting.Id ?? channel);
        var item = new NotificationChannelSettingItem(
            id,
            channel,
            Required(setting.Provider, "Provider is required."),
            Required(setting.SenderId, "Sender id is required."),
            Required(setting.Format, "Format is required."),
            string.IsNullOrWhiteSpace(setting.Status) ? "Active" : setting.Status.Trim(),
            NormalizeOptional(setting.Description));

        var saved = await UpsertAsync(
            new NotificationSettingUpsert(
                $"channel-settings:{id}",
                ChannelSettingsCategory,
                JsonSerializer.Serialize(item, PayloadJsonOptions),
                IsSecret: false,
                item.Description,
                setting.Actor),
            ct);

        return DeserializePayload<NotificationChannelSettingItem>(saved.Value) ?? item;
    }

    public async Task<IReadOnlyList<NotificationLifecycleEventItem>> ListLifecycleEventsAsync(CancellationToken ct)
    {
        var items = await ListPayloadSettingsAsync<NotificationLifecycleEventItem>(LifecycleEventsCategory, ct);
        return items.Length == 0
            ? DefaultLifecycleEvents()
            : items.Select(WithTemplateVariables).ToArray();
    }

    public async Task<IReadOnlyList<NotificationRetryPolicyItem>> ListRetryPoliciesAsync(CancellationToken ct)
    {
        var items = await ListPayloadSettingsAsync<NotificationRetryPolicyItem>(RetryPoliciesCategory, ct);
        return items.Length == 0 ? DefaultRetryPolicies() : items;
    }

    public async Task<NotificationRetryPolicyItem> UpsertRetryPolicyAsync(NotificationRetryPolicyUpsert policy, CancellationToken ct)
    {
        var eventType = Required(policy.EventType, "Event type is required.");
        var id = Slug(policy.Id ?? eventType);
        var item = new NotificationRetryPolicyItem(
            id,
            eventType,
            Required(policy.Channel, "Channel is required."),
            policy.MaxRetries ?? throw new NotificationRequestValidationException("Maximum retries is required."),
            policy.DelayMin ?? throw new NotificationRequestValidationException("Retry delay is required."),
            Required(policy.Strategy, "Retry strategy is required."),
            string.IsNullOrWhiteSpace(policy.Status) ? "Active" : policy.Status.Trim(),
            NormalizeOptional(policy.Description));

        var saved = await UpsertAsync(
            new NotificationSettingUpsert(
                $"retry-policies:{id}",
                RetryPoliciesCategory,
                JsonSerializer.Serialize(item, PayloadJsonOptions),
                IsSecret: false,
                item.Description,
                policy.Actor),
            ct);

        return DeserializePayload<NotificationRetryPolicyItem>(saved.Value) ?? item;
    }

    private static string? NormalizeKey(string? key)
        => string.IsNullOrWhiteSpace(key) ? null : key.Trim();

    private static NotificationSettingItem ToItem(NotificationSettingModel model)
        => new(
            model.Key,
            model.Category,
            model.IsSecret ? "********" : model.Value,
            model.IsSecret,
            model.Description,
            model.UpdatedUtc,
            model.UpdatedBy);

    private async Task<T[]> ListPayloadSettingsAsync<T>(string category, CancellationToken ct)
    {
        var settings = await ListAsync(category, ct);
        return settings
            .Where(x => !x.IsSecret)
            .Select(x => DeserializePayload<T>(x.Value))
            .Where(x => x is not null)
            .Cast<T>()
            .ToArray();
    }

    private static T? DeserializePayload<T>(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(value, PayloadJsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string Required(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new NotificationRequestValidationException(message);

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Slug(string value)
        => string.Join(
            "-",
            value.Trim()
                .ToLowerInvariant()
                .Split([' ', '_', ':', '/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static IReadOnlyList<NotificationChannelSettingItem> DefaultChannelSettings()
        =>
        [
            new("email", "Email", "Graph", "Configured sender", "Plain text or HTML", "Active", null),
            new("sms", "SMS", "SMS provider", "AFH", "Text", "Active", null),
            new("in-app", "In-app", "MFTL Control Centre", "System notification", "Portal message", "Active", null)
        ];

    private static IReadOnlyList<NotificationLifecycleEventItem> DefaultLifecycleEvents()
        =>
        [
            Event("booking-confirmed", "Booking Confirmed", "Booking Service", "Sent after a booking is confirmed and recipients are resolved.", "Active"),
            Event("booking-reminder", "Booking Reminder", "Notification Service", "Sent before the appointment when reminder settings are enabled.", "Active"),
            Event("booking-cancelled", "Booking Cancelled", "Booking Service", "Sent when a booking is cancelled.", "Active"),
            Event("booking-rearranged", "Booking Rearranged", "Booking Service", "Sent when appointment date, time, or adviser changes.", "Active"),
            Event("adviser-request-outcome", "Adviser Request Outcome", "Booking Service", "Sent when an adviser change request is approved or rejected.", "Active"),
            Event("delivery-failed", "Delivery Failed", "Notification Service", "Creates failed dispatch records and drives retry policy.", "Active")
        ];

    private static NotificationLifecycleEventItem Event(
        string id,
        string @event,
        string owner,
        string description,
        string status)
        => new(id, @event, owner, description, status, NotificationTemplateVariableCatalog.ForLifecycleEvent(@event));

    private static NotificationLifecycleEventItem WithTemplateVariables(NotificationLifecycleEventItem item)
    {
        var variables = NotificationTemplateVariableCatalog.ForLifecycleEvent(item.Event);
        return variables.Count == 0 ? item : item with { Variables = variables };
    }

    private static IReadOnlyList<NotificationRetryPolicyItem> DefaultRetryPolicies()
        =>
        [
            new("email-delivery-failure", "Email Delivery Failure", "Email", 3, 15, "Exponential backoff", "Active", null),
            new("sms-delivery-failure", "SMS Delivery Failure", "SMS", 2, 5, "Linear", "Active", null),
            new("in-app-delivery-failure", "In-app Delivery Failure", "In-app", 1, 10, "Single retry", "Active", null),
            new("provider-outage", "Provider Outage", "All", 6, 30, "Exponential backoff", "Active", null)
        ];
}
