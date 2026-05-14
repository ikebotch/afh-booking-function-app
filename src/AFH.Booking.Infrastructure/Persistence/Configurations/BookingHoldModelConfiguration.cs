using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class BookingHoldModelConfiguration : IEntityTypeConfiguration<BookingHoldModel>
{
    public void Configure(EntityTypeBuilder<BookingHoldModel> b)
    {
        b.ToTable("BookingHolds");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .HasMaxLength(64)
            .IsRequired();
        b.HasIndex(x => new { x.SlotId, x.Status });
        // -----
        // FK to Slot (one hold per slot)
        // -----
        b.Property(x => x.SlotId)
            .HasMaxLength(64)
            .IsRequired();

        // Only live holds block a slot. Historical cancelled/released/expired rows remain for audit.
        b.HasIndex(x => x.SlotId)
            .IsUnique()
            .HasFilter("[Status] IN (0, 1)");

        b.HasOne(x => x.Slot)
            .WithMany()
            .HasForeignKey<BookingHoldModel>(x => x.SlotId)
            .OnDelete(DeleteBehavior.Cascade);

        // -----
        // Status + lifecycle
        // -----
        b.Property(x => x.Status)
            .IsRequired();

        b.Property(x => x.CreatedUtc)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        b.Property(x => x.HoldExpiresUtc)
            .IsRequired();

        // -----
        // Calendar (optional)
        // -----
        b.Property(x => x.CalendarProviderEventId)
            .HasMaxLength(256);

        // -----
        // Confirm/Cancel (optional)
        // -----
        b.Property(x => x.ConfirmedUtc);

        b.Property(x => x.CancelledUtc);

        b.Property(x => x.CancelReason)
            .HasMaxLength(512);

        // Helpful indexes for active-hold queries
        b.HasIndex(x => x.HoldExpiresUtc);
        b.HasIndex(x => x.Status);


        b.Property(x => x.RowVersion)
   .IsRowVersion()
   .ValueGeneratedOnAddOrUpdate();
    }
}
