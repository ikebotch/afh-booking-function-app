using AFH.Notification.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Notification.Infrastructure.Persistence.Configurations;

public sealed class NotificationDispatchModelConfiguration : IEntityTypeConfiguration<NotificationDispatchModel>
{
    public void Configure(EntityTypeBuilder<NotificationDispatchModel> b)
    {
        b.ToTable("NotificationDispatches", "dbo");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasMaxLength(64).IsRequired();
        b.Property(x => x.DispatchUid).IsRequired();
        b.Property(x => x.BookingId).HasMaxLength(64);
        b.Property(x => x.TransactionId).HasMaxLength(64);
        b.Property(x => x.TransactionRef).HasMaxLength(128);
        b.Property(x => x.LifecycleEventId).HasMaxLength(64);
        b.Property(x => x.CorrelationId).HasMaxLength(150);
        b.Property(x => x.EventType).HasMaxLength(64);
        b.Property(x => x.SmsStatus).HasMaxLength(32);
        b.Property(x => x.EmailStatus).HasMaxLength(32);
        b.Property(x => x.OutcomeCode).HasMaxLength(64);
        b.Property(x => x.RecipientType).HasMaxLength(100);
        b.Property(x => x.RecipientPhone).HasMaxLength(64);
        b.Property(x => x.RecipientEmail).HasMaxLength(320);
        b.Property(x => x.ProviderMessageId).HasMaxLength(200);
        b.Property(x => x.MessageSubject).HasMaxLength(500);
        b.Property(x => x.SourceApplication).HasMaxLength(100);
        b.Property(x => x.SourceReferenceType).HasMaxLength(100);
        b.Property(x => x.SourceReferenceId).HasMaxLength(150);
        b.Property(x => x.NotificationType).HasMaxLength(150);
        b.Property(x => x.RecipientMobile).HasMaxLength(50);
        b.Property(x => x.Channel).HasMaxLength(50);
        b.Property(x => x.ProviderName).HasMaxLength(100);
        b.Property(x => x.TemplateName).HasMaxLength(200);
        b.Property(x => x.TemplateKey).HasMaxLength(150);
        b.Property(x => x.TemplateVersion).HasMaxLength(50);
        b.Property(x => x.Status).HasMaxLength(50);

        b.HasIndex(x => x.BookingId);
        b.HasIndex(x => x.TransactionId);
        b.HasIndex(x => x.LifecycleEventId);
        b.HasIndex(x => x.CreatedUtc);
        b.HasIndex(x => x.ProviderMessageId);
        b.HasIndex(x => x.NotificationOutboxId);
        b.HasIndex(x => x.DispatchUid).IsUnique();
        b.HasIndex(x => new { x.RecipientEmail, x.CreatedUtc });
        b.HasIndex(x => new { x.Status, x.CreatedUtc });
        b.HasIndex(x => new { x.NotificationType, x.CreatedUtc });
        b.HasIndex(x => new { x.SourceApplication, x.SourceReferenceType, x.SourceReferenceId });

        b.HasOne<NotificationOutboxModel>()
            .WithMany()
            .HasForeignKey(x => x.NotificationOutboxId)
            .HasConstraintName("FK_NotificationDispatches_NotificationOutbox_NotificationOutboxId");
    }
}
