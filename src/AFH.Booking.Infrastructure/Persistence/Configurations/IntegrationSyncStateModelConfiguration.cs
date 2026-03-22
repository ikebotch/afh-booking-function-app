using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class IntegrationSyncStateModelConfiguration : IEntityTypeConfiguration<IntegrationSyncStateModel>
{
    public void Configure(EntityTypeBuilder<IntegrationSyncStateModel> b)
    {
        b.ToTable("IntegrationSyncStates");
        b.HasKey(x => x.Key);

        b.Property(x => x.Key).HasMaxLength(128);
        b.Property(x => x.Value).IsRequired();
        b.Property(x => x.UpdatedUtc).IsRequired();
        b.HasIndex(x => x.UpdatedUtc);
    }
}
