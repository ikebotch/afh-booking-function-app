using AFH.Notification.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Notification.Infrastructure.Persistence.Configurations;

public sealed class NotificationMessageLogModelConfiguration : IEntityTypeConfiguration<NotificationMessageLogModel>
{
    public void Configure(EntityTypeBuilder<NotificationMessageLogModel> b)
    {
        b.ToTable("NotificationMessageLogs", "dbo");
        b.HasKey(x => x.Id);

        b.Property(x => x.NotificationDispatchId).IsRequired();
        b.Property(x => x.SourceApplication).HasMaxLength(100);
        b.Property(x => x.NotificationType).HasMaxLength(150);
        b.Property(x => x.CorrelationId).HasMaxLength(150);
        b.Property(x => x.RecipientType).HasMaxLength(100);
        b.Property(x => x.RecipientEmail).HasMaxLength(320);
        b.Property(x => x.RecipientMobile).HasMaxLength(50);
        b.Property(x => x.Channel).HasMaxLength(50).IsRequired();
        b.Property(x => x.TemplateKey).HasMaxLength(150).IsRequired();
        b.Property(x => x.TemplateVersion).HasMaxLength(50).IsRequired();
        b.Property(x => x.Subject).HasMaxLength(500);
        b.Property(x => x.Body).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(50).IsRequired();
        b.Property(x => x.BodyHash).HasMaxLength(128);
        b.Property(x => x.CreatedUtc).IsRequired();

        b.HasIndex(x => x.NotificationDispatchId).IsUnique();
        b.HasIndex(x => x.NotificationOutboxId);
        b.HasIndex(x => new { x.RecipientEmail, x.CreatedUtc });
        b.HasIndex(x => new { x.NotificationType, x.CreatedUtc });

        b.HasOne<NotificationDispatchModel>()
            .WithMany()
            .HasForeignKey(x => x.NotificationDispatchId)
            .HasPrincipalKey(x => x.DispatchUid)
            .HasConstraintName("FK_NotificationMessageLogs_NotificationDispatches_NotificationDispatchId");

        b.HasOne<NotificationOutboxModel>()
            .WithMany()
            .HasForeignKey(x => x.NotificationOutboxId)
            .HasConstraintName("FK_NotificationMessageLogs_NotificationOutbox_NotificationOutboxId");

        b.HasOne<NotificationTemplateModel>()
            .WithMany()
            .HasForeignKey(x => x.TemplateContentId)
            .HasConstraintName("FK_NotificationMessageLogs_NotificationTemplates_TemplateContentId");
    }
}
