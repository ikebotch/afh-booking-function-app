using AFH.Booking.Infrastructure.Persistence.Configurations;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Persistence;

public sealed class NotificationPolicyDbContext : DbContext
{
    public NotificationPolicyDbContext(DbContextOptions<NotificationPolicyDbContext> options) : base(options) { }

    public DbSet<NotificationRuleModel> NotificationRules => Set<NotificationRuleModel>();
    public DbSet<NotificationRuleChannelModel> NotificationRuleChannels => Set<NotificationRuleChannelModel>();
    public DbSet<NotificationRuleRecipientModel> NotificationRuleRecipients => Set<NotificationRuleRecipientModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new NotificationRuleModelConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationRuleChannelModelConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationRuleRecipientModelConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
