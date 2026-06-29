using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class BookingReferenceAllocationModelConfiguration : IEntityTypeConfiguration<BookingReferenceAllocationModel>
{
    public void Configure(EntityTypeBuilder<BookingReferenceAllocationModel> b)
    {
        b.ToTable("BookingReferenceAllocations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64).IsRequired();
        b.Property(x => x.Value)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEXT VALUE FOR dbo.BookingReferenceNumber");
        b.Property(x => x.CreatedUtc).IsRequired();
        b.HasIndex(x => x.CreatedUtc);
    }
}
