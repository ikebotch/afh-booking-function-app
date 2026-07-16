using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class AdviserSkillProjectionModelConfiguration : IEntityTypeConfiguration<AdviserSkillProjectionModel>
{
    public void Configure(EntityTypeBuilder<AdviserSkillProjectionModel> b)
    {
        b.ToTable("AdviserSkillProjections");
        b.HasKey(x => new { x.AdviserId, x.SkillCode });

        b.Property(x => x.AdviserId).HasMaxLength(256).IsRequired();
        b.Property(x => x.SkillCode).HasMaxLength(256).IsRequired();
        b.Property(x => x.SkillLabel).HasMaxLength(256).IsRequired();
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.CreatedUtc).IsRequired();
        b.Property(x => x.UpdatedUtc);
        b.Property(x => x.LastSyncedUtc).IsRequired();
        b.Property(x => x.SourceVersion).HasMaxLength(128);

        b.HasIndex(x => new { x.SkillCode, x.IsActive });
        b.HasIndex(x => new { x.AdviserId, x.IsActive });
        b.HasIndex(x => x.LastSyncedUtc);
    }
}


