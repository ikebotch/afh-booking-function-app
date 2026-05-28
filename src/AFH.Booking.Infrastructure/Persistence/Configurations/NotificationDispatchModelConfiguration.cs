using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class NotificationDispatchModelConfiguration : IEntityTypeConfiguration<NotificationDispatchModel>
{
    public void Configure(EntityTypeBuilder<NotificationDispatchModel> b)
    {
        b.ToTable("NotificationDispatches");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasMaxLength(64).IsRequired();
        b.Property(x => x.BookingId).HasMaxLength(64).IsRequired();
        b.Property(x => x.TransactionId).HasMaxLength(64);
        b.Property(x => x.TransactionRef).HasMaxLength(128);
        b.Property(x => x.LifecycleEventId).HasMaxLength(64);
        b.Property(x => x.CorrelationId).HasMaxLength(128);
        b.Property(x => x.EventType).HasMaxLength(64).IsRequired();
        b.Property(x => x.SmsStatus).HasMaxLength(32).IsRequired();
        b.Property(x => x.EmailStatus).HasMaxLength(32).IsRequired();
        b.Property(x => x.OutcomeCode).HasMaxLength(64).IsRequired();
        b.Property(x => x.FailureDetails).HasMaxLength(2048);
        b.Property(x => x.RecipientPhone).HasMaxLength(64);
        b.Property(x => x.RecipientEmail).HasMaxLength(256);
        b.Property(x => x.ProviderMessageId).HasMaxLength(128);
        b.Property(x => x.MessageBody).HasMaxLength(4000);
        b.Property(x => x.SourceApplication).HasMaxLength(100);
        b.Property(x => x.NotificationType).HasMaxLength(150);
        b.Property(x => x.Channel).HasMaxLength(50);
        b.Property(x => x.ProviderName).HasMaxLength(100);
        b.Property(x => x.TemplateName).HasMaxLength(200);
        b.Property(x => x.TemplateKey).HasMaxLength(150);
        b.Property(x => x.TemplateVersion).HasMaxLength(50);

        b.HasIndex(x => x.BookingId);
        b.HasIndex(x => x.TransactionId);
        b.HasIndex(x => x.LifecycleEventId);
        b.HasIndex(x => x.CreatedUtc);
        b.HasIndex(x => x.ProviderMessageId);
        b.HasIndex(x => x.NotificationOutboxId);
    }
}
