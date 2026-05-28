using AFH.Notification.Infrastructure.Persistence.Configurations;
using AFH.Notification.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Notification.Infrastructure.Persistence;

public sealed class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    public DbSet<NotificationOutboxModel> NotificationOutbox => Set<NotificationOutboxModel>();
    public DbSet<NotificationDispatchModel> NotificationDispatches => Set<NotificationDispatchModel>();
    public DbSet<EmailBounceEventModel> EmailBounceEvents => Set<EmailBounceEventModel>();
    public DbSet<NotificationTemplateModel> NotificationTemplates => Set<NotificationTemplateModel>();
    public DbSet<NotificationMessageLogModel> NotificationMessageLogs => Set<NotificationMessageLogModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new NotificationOutboxConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationDispatchModelConfiguration());
        modelBuilder.ApplyConfiguration(new EmailBounceEventModelConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationTemplateModelConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationMessageLogModelConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
