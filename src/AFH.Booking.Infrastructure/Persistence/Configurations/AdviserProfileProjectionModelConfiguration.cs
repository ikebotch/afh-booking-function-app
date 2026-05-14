using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class AdviserProfileProjectionModelConfiguration : IEntityTypeConfiguration<AdviserProfileProjectionModel>
{
    public void Configure(EntityTypeBuilder<AdviserProfileProjectionModel> b)
    {
        b.ToTable("AdviserProfileProjections");
        b.HasKey(x => x.AdviserId);

        b.Property(x => x.AdviserId).HasMaxLength(256);
        b.Property(x => x.DisplayName).IsRequired().HasMaxLength(256);
        b.Property(x => x.MailboxUserId).HasMaxLength(256);
        b.Property(x => x.Region).HasMaxLength(128);
        b.Property(x => x.HomePostcode).HasMaxLength(32);
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.Rating).IsRequired();
        b.Property(x => x.SkillsJson).IsRequired();
        b.Property(x => x.SourceVersion).HasMaxLength(128);
        b.Property(x => x.LastSyncedUtc).IsRequired();

        b.HasIndex(x => new { x.IsActive, x.Region });
        b.HasIndex(x => x.LastSyncedUtc);
    }
}
