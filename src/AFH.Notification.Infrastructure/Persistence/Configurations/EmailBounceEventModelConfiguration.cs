using AFH.Notification.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Notification.Infrastructure.Persistence.Configurations;

public sealed class EmailBounceEventModelConfiguration : IEntityTypeConfiguration<EmailBounceEventModel>
{
    public void Configure(EntityTypeBuilder<EmailBounceEventModel> b)
    {
        b.ToTable("EmailBounceEvents", "dbo");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasMaxLength(64).IsRequired();
        b.Property(x => x.ProviderMessageId).HasMaxLength(200);
        b.Property(x => x.RecipientEmail).HasMaxLength(320);
        b.Property(x => x.ReasonCode).HasMaxLength(128);
        b.Property(x => x.ReasonDetail).HasMaxLength(2048);

        b.HasIndex(x => x.ProviderMessageId);
        b.HasIndex(x => x.RecipientEmail);
        b.HasIndex(x => x.ReceivedUtc);
    }
}
