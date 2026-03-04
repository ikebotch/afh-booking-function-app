using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class LocationModelConfiguration : IEntityTypeConfiguration<LocationModel>
{
    public void Configure(EntityTypeBuilder<LocationModel> b)
    {
        b.ToTable("Locations");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasMaxLength(64).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();

        b.Property(x => x.AddressLine1).HasMaxLength(256);
        b.Property(x => x.City).HasMaxLength(128);
        b.Property(x => x.Postcode).HasMaxLength(32);
        b.Property(x => x.Address).HasMaxLength(512);

        b.Property(x => x.IsActive).IsRequired();

        b.Property(x => x.CreatedUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        b.Property(x => x.UpdatedUtc);

        b.HasIndex(x => x.DisplayName);
        b.HasIndex(x => new { x.City, x.Postcode });
    }
}