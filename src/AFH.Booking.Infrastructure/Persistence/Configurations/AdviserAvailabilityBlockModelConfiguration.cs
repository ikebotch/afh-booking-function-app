using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class AdviserAvailabilityBlockModelConfiguration : IEntityTypeConfiguration<AdviserAvailabilityBlockModel>
{
    public void Configure(EntityTypeBuilder<AdviserAvailabilityBlockModel> b)
    {
        b.ToTable("AdviserAvailabilityBlocks");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.AdviserId).IsRequired().HasMaxLength(256);
        b.Property(x => x.ProviderEventId).IsRequired().HasMaxLength(512);
        b.Property(x => x.CalendarId).HasMaxLength(512);
        b.Property(x => x.Subject).HasMaxLength(512);
        b.Property(x => x.ChangeKey).HasMaxLength(256);
        b.Property(x => x.ICalUId).HasMaxLength(512);
        b.Property(x => x.SourceReceiptId).HasMaxLength(64);

        b.Property(x => x.StartUtc).IsRequired();
        b.Property(x => x.EndUtc).IsRequired();
        b.Property(x => x.IsCancelled).IsRequired();
        b.Property(x => x.LastSyncedUtc).IsRequired();

        b.HasIndex(x => new { x.AdviserId, x.ProviderEventId }).IsUnique();
        b.HasIndex(x => new { x.AdviserId, x.StartUtc, x.EndUtc });
        b.HasIndex(x => new { x.AdviserId, x.LastSyncedUtc });
    }
}
