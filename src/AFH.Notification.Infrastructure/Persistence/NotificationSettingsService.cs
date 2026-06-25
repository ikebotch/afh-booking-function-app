using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Notification.Infrastructure.Persistence;

public sealed class NotificationSettingsService : INotificationSettingsService
{
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
}
