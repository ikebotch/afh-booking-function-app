using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class FeatureFlagModelConfiguration : IEntityTypeConfiguration<FeatureFlagModel>
{
    public void Configure(EntityTypeBuilder<FeatureFlagModel> b)
    {
        b.ToTable("FeatureFlags");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(150).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.UpdatedBy).HasMaxLength(150);
    }
}
