using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class BookingSlotModelConfiguration : IEntityTypeConfiguration<BookingSlotModel>
{
    public void Configure(EntityTypeBuilder<BookingSlotModel> b)
    {
        b.ToTable("BookingSlots");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .HasMaxLength(64)
            .IsRequired();

        b.Property(x => x.TransactionId)
            .HasMaxLength(64)
            .IsRequired();

        b.Property(x => x.AdviserId)
            .HasMaxLength(64)
            .IsRequired();

        b.Property(x => x.AdviserName)
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.StartUtc)
            .IsRequired();

        b.Property(x => x.EndUtc)
            .IsRequired();

        b.Property(x => x.Score)
            .IsRequired();

        // store score breakdown dict as JSON string
        b.Property(x => x.ScoreBreakdownJson)
            .HasMaxLength(4000);

        b.Property(x => x.TravelMinutes);
        b.Property(x => x.CompanyBufferMinutes);
        b.Property(x => x.DistanceMiles).HasColumnType("decimal(9,2)");
        b.Property(x => x.TravelDistanceMiles);

        b.Property(x => x.SourceLocationRef)
            .HasMaxLength(128);

        b.Property(x => x.SourcePostcode)
            .HasMaxLength(32);
        b.Property(x => x.SourceLatitude);
        b.Property(x => x.SourceLongitude);

        b.Property(x => x.DestinationLocationRef)
            .HasMaxLength(128);

        b.Property(x => x.DestinationPostcode)
            .HasMaxLength(32);
        b.Property(x => x.DestinationLatitude);
        b.Property(x => x.DestinationLongitude);

        b.Property(x => x.TravelProvider)
            .HasMaxLength(64);

        b.Property(x => x.TravelConfidence)
            .HasMaxLength(32);

        b.Property(x => x.TravelCalculatedUtc);

        b.Property(x => x.TravelStatus)
            .HasMaxLength(32);

        b.Property(x => x.TravelMessage)
            .HasMaxLength(512);

        // renamed
        b.Property(x => x.LocationRef)
            .HasMaxLength(64);

        b.Property(x => x.CreatedUtc)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        // Relationship: Slot -> Hold (1-0/1)
        // Hold has FK SlotId -> Slot.Id, unique.
        b.HasOne(x => x.Hold)
            .WithOne(x => x.Slot)
            .HasForeignKey<BookingHoldModel>(x => x.SlotId)
            .OnDelete(DeleteBehavior.Cascade);

        // Helpful indexes
        b.HasIndex(x => new { x.TransactionId, x.StartUtc });
        b.HasIndex(x => x.AdviserId);
    }
}
