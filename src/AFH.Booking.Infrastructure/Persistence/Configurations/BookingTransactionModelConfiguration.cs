using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class BookingTransactionModelConfiguration : IEntityTypeConfiguration<BookingTransactionModel>
{
    public void Configure(EntityTypeBuilder<BookingTransactionModel> b)
    {
        b.ToTable("BookingTransactions");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .HasMaxLength(64)
            .IsRequired();

        b.Property(x => x.TransactionRef)
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.ProposedStartUtc)
            .IsRequired();

        b.Property(x => x.DurationMinutes)
            .IsRequired();

        b.Property(x => x.Timezone)
            .HasMaxLength(64)
            .IsRequired();

        b.Property(x => x.IsRemote)
            .IsRequired();

        b.Property(x => x.MeetingType)
            .HasMaxLength(128);

        b.Property(x => x.LocationRef)
            .HasMaxLength(128);

        b.Property(x => x.Status)
            .IsRequired();

        b.Property(x => x.CreatedUtc)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        b.Property(x => x.ExpiresUtc);

        // Relationship: Transaction 1 -> many Slots
        b.HasMany(x => x.Slots)
            .WithOne(x => x.Transaction)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes (helps query by external ref)
        b.HasIndex(x => x.TransactionRef);
        b.HasIndex(x => x.CreatedUtc);

        b.Property(x => x.RowVersion)
        .IsRowVersion()
        .ValueGeneratedOnAddOrUpdate();

    }
}