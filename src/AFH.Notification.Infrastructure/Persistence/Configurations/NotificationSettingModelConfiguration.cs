using AFH.Notification.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Notification.Infrastructure.Persistence.Configurations;

public sealed class NotificationSettingModelConfiguration : IEntityTypeConfiguration<NotificationSettingModel>
{
    public void Configure(EntityTypeBuilder<NotificationSettingModel> b)
    {
        b.ToTable("NotificationSettings", "dbo");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(150).IsRequired();
        b.Property(x => x.Category).HasMaxLength(100).IsRequired();
        b.Property(x => x.Value).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.UpdatedBy).HasMaxLength(150);
        b.HasIndex(x => x.Category);
    }
}
