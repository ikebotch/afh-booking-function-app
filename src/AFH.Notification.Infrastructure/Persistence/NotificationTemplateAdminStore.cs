using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Notification.Infrastructure.Persistence;

public sealed class NotificationTemplateAdminStore : INotificationTemplateAdminStore
{
    private readonly NotificationDbContext _db;

    public NotificationTemplateAdminStore(NotificationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<NotificationTemplateSummary>> ListAsync(NotificationTemplateQuery query, CancellationToken ct)
    {
        var rows = _db.NotificationTemplates.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.TemplateKey))
            rows = rows.Where(x => x.TemplateKey == query.TemplateKey.Trim());
        if (query.Channel is { } channel)
            rows = rows.Where(x => x.Channel == channel.ToString());
        if (query.IsActive is { } isActive)
            rows = rows.Where(x => x.IsActive == isActive);

        return await rows
            .OrderBy(x => x.TemplateKey)
            .ThenBy(x => x.TemplateVersion)
            .ThenBy(x => x.Channel)
            .Select(x => new NotificationTemplateSummary(
                x.Id,
                x.TemplateKey,
                x.TemplateVersion,
                ParseChannel(x.Channel),
                x.Name,
                x.Description,
                x.ContentType,
                x.IsActive,
                x.UpdatedUtc))
            .ToArrayAsync(ct);
    }

    public async Task<NotificationTemplateAdminItem?> GetAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.NotificationTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : Map(row);
    }

    public async Task<NotificationTemplateAdminItem?> GetAsync(string templateKey, string templateVersion, NotificationChannel channel, CancellationToken ct)
    {
        var row = await _db.NotificationTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(x =>
                x.TemplateKey == templateKey &&
                x.TemplateVersion == templateVersion &&
                x.Channel == channel.ToString(), ct);

        return row is null ? null : Map(row);
    }

    public Task<bool> ExistsAsync(string templateKey, string templateVersion, NotificationChannel channel, Guid? excludingId, CancellationToken ct)
        => _db.NotificationTemplates.AnyAsync(x =>
            x.TemplateKey == templateKey &&
            x.TemplateVersion == templateVersion &&
            x.Channel == channel.ToString() &&
            (excludingId == null || x.Id != excludingId.Value), ct);

    public async Task<NotificationTemplateAdminItem> CreateAsync(NotificationTemplateUpsert template, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var row = new NotificationTemplateModel
        {
            Id = Guid.NewGuid(),
            TemplateKey = template.TemplateKey,
            TemplateVersion = template.TemplateVersion,
            Channel = template.Channel.ToString(),
            Name = template.Name,
            Description = template.Description,
            SubjectTemplate = template.SubjectTemplate,
            BodyTemplate = template.BodyTemplate,
            ContentType = template.ContentType,
            IsActive = template.IsActive,
            CreatedBy = template.Actor,
            UpdatedBy = template.Actor,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        _db.NotificationTemplates.Add(row);
        await _db.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<NotificationTemplateAdminItem> UpdateAsync(Guid id, NotificationTemplateUpsert template, CancellationToken ct)
    {
        var row = await _db.NotificationTemplates.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotificationRequestValidationException("Template was not found.");

        row.TemplateKey = template.TemplateKey;
        row.TemplateVersion = template.TemplateVersion;
        row.Channel = template.Channel.ToString();
        row.Name = template.Name;
        row.Description = template.Description;
        row.SubjectTemplate = template.SubjectTemplate;
        row.BodyTemplate = template.BodyTemplate;
        row.ContentType = template.ContentType;
        row.IsActive = template.IsActive;
        row.UpdatedBy = template.Actor;
        row.UpdatedUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<NotificationTemplateAdminItem> SetActiveAsync(Guid id, bool isActive, string? actor, CancellationToken ct)
    {
        var row = await _db.NotificationTemplates.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotificationRequestValidationException("Template was not found.");

        row.IsActive = isActive;
        row.UpdatedBy = string.IsNullOrWhiteSpace(actor) ? row.UpdatedBy : actor.Trim();
        row.UpdatedUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Map(row);
    }

    private static NotificationTemplateAdminItem Map(NotificationTemplateModel row)
        => new(
            row.Id,
            row.TemplateKey,
            row.TemplateVersion,
            ParseChannel(row.Channel),
            row.Name,
            row.Description,
            row.SubjectTemplate,
            row.BodyTemplate,
            row.ContentType,
            row.IsActive,
            row.CreatedUtc,
            row.UpdatedUtc);

    private static NotificationChannel ParseChannel(string value)
        => Enum.TryParse<NotificationChannel>(value, ignoreCase: true, out var channel)
            ? channel
            : NotificationChannel.Unknown;
}
