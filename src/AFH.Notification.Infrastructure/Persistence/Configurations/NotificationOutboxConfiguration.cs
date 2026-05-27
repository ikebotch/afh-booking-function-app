using AFH.Notification.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Notification.Infrastructure.Persistence.Configurations;

public sealed class NotificationOutboxConfiguration : IEntityTypeConfiguration<NotificationOutboxModel>
{
    public void Configure(EntityTypeBuilder<NotificationOutboxModel> builder)
    {
        builder.ToTable("NotificationOutbox");
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.SourceApplication)
            .HasMaxLength(100)
            .IsRequired();
            
        builder.Property(x => x.NotificationType)
            .HasMaxLength(150)
            .IsRequired();
            
        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(500)
            .IsRequired();
            
        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique();
            
        builder.Property(x => x.Status)
            .HasMaxLength(50)
            .IsRequired();
            
        builder.Property(x => x.QueueMessageId)
            .HasMaxLength(200);
            
        builder.Property(x => x.PayloadJson)
            .IsRequired();
            
        builder.Property(x => x.AttemptCount)
            .IsRequired();
            
        builder.Property(x => x.CreatedUtc)
            .IsRequired();
            
        builder.Property(x => x.UpdatedUtc)
            .IsRequired();
            
        builder.HasIndex(x => new { x.Status, x.CreatedUtc });
    }
}
