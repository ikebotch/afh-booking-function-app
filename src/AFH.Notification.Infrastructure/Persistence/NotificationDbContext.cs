using AFH.Notification.Infrastructure.Persistence.Configurations;
using AFH.Notification.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Notification.Infrastructure.Persistence;

public sealed class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    public DbSet<NotificationOutboxModel> NotificationOutbox => Set<NotificationOutboxModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new NotificationOutboxConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
