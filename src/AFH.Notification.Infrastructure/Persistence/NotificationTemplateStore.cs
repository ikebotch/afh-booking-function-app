using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AFH.Notification.Infrastructure.Persistence;

public sealed class NotificationTemplateStore : INotificationTemplateStore
{
    private readonly NotificationDbContext _db;

    public NotificationTemplateStore(NotificationDbContext db)
    {
        _db = db;
    }

    public async Task<NotificationTemplateDefinition?> GetAsync(
        string templateKey,
        string templateVersion,
        NotificationChannel channel,
        CancellationToken ct)
    {
        var channelName = channel.ToString();
        var row = await _db.NotificationTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(x =>
                x.TemplateKey == templateKey &&
                x.TemplateVersion == templateVersion &&
                x.Channel == channelName &&
                x.IsActive,
                ct);

        return row is null
            ? null
            : new NotificationTemplateDefinition(
                row.TemplateKey,
                row.TemplateVersion,
                channel,
                row.Name,
                row.Description,
                row.SubjectTemplate,
                row.BodyTemplate,
                row.ContentType,
                row.IsActive);
    }
}
