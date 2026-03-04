using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class BookingEntityConfiguration : IEntityTypeConfiguration<BookingsModel>
{
    public void Configure(EntityTypeBuilder<BookingsModel> b)
    {
        b.ToTable("Bookings");

        // Key (value object -> string)
        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .HasConversion(
                v => v.Value,
                v => new BookingId(v))
            .HasMaxLength(64);

        b.Property(x => x.AdviserId).HasMaxLength(256).IsRequired();   // UPN can be > 64
        b.Property(x => x.CustomerId).HasMaxLength(256).IsRequired();

        b.Property(x => x.Subject).HasMaxLength(256);

        b.Property(x => x.StartUtc).IsRequired();
        b.Property(x => x.EndUtc).IsRequired();

        b.Property(x => x.Timezone).HasMaxLength(64).IsRequired();

        b.Property(x => x.Mode).IsRequired();
        b.Property(x => x.Status).IsRequired();

        b.Property(x => x.ProviderEventId).HasMaxLength(256);
        b.Property(x => x.HoldExpiresUtc);

        b.Property(x => x.Notes).HasMaxLength(4000);
        b.Property(x => x.TransactionId).HasMaxLength(128);
        b.Property(x => x.ProviderEventId).HasMaxLength(256);

        // CreatedUtc: pick ONE approach

        // Option A (recommended): DB default (no null inserts)
        b.Property(x => x.CreatedUtc)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .ValueGeneratedOnAdd();

        // Option B: application sets it (then remove defaultValueSql)
        // b.Property(x => x.CreatedUtc).IsRequired();

        // Owned value object (IMPORTANT)
        b.OwnsOne(x => x.Location, loc =>
        {
            // Flattened columns (optional but tidy)
            loc.Property(x => x.DisplayName).HasColumnName("LocationDisplayName").HasMaxLength(256);
            loc.Property(x => x.AddressLine1).HasColumnName("LocationAddressLine1").HasMaxLength(256);
            loc.Property(x => x.City).HasColumnName("LocationCity").HasMaxLength(128);
            loc.Property(x => x.Postcode).HasColumnName("LocationPostcode").HasMaxLength(32);
         
        });

        b.Navigation(x => x.Location).IsRequired(false);

        // Concurrency token
        b.Property(x => x.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();
    }
}