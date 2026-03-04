using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configuration;

public sealed class CalendarEventSnapshotModelConfig : IEntityTypeConfiguration<CalendarEventSnapshotModel>
{
    public void Configure(EntityTypeBuilder<CalendarEventSnapshotModel> b)
    {
        b.ToTable("CalendarEventSnapshots");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64);

        b.Property(x => x.ReceiptId).IsRequired().HasMaxLength(64);

        b.Property(x => x.UserId).IsRequired().HasMaxLength(256);
        b.Property(x => x.ProviderEventId).IsRequired().HasMaxLength(512);

        b.Property(x => x.CalendarId).HasMaxLength(512);
        b.Property(x => x.Subject).HasMaxLength(512);

    
        b.Property(x => x.ChangeKey).HasMaxLength(256);
        b.Property(x => x.ICalUId).HasMaxLength(512);

        b.Property(x => x.FetchedUtc).IsRequired();
        b.Property(x => x.FetchError).HasMaxLength(512);

        b.HasOne(x => x.Receipt)
         .WithMany(r => r.Snapshots)
         .HasForeignKey(x => x.ReceiptId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.UserId, x.ProviderEventId, x.FetchedUtc });
        b.HasIndex(x => x.ReceiptId);

     
         b.HasIndex(x => new { x.UserId, x.ICalUId });
    }
}