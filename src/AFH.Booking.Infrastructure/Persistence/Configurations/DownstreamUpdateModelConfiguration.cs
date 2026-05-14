using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class DownstreamUpdateModelConfiguration : IEntityTypeConfiguration<DownstreamUpdateModel>
{
    public void Configure(EntityTypeBuilder<DownstreamUpdateModel> b)
    {
        b.ToTable("DownstreamUpdates");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasMaxLength(64).IsRequired();
        b.Property(x => x.BookingId).HasMaxLength(64).IsRequired();
        b.Property(x => x.ChangeType).HasMaxLength(64).IsRequired();
        b.Property(x => x.TransactionRef).HasMaxLength(256).IsRequired();
        b.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(x => x.Status).HasMaxLength(32).IsRequired();
        b.Property(x => x.ErrorMessage).HasMaxLength(2048);

        b.HasIndex(x => new { x.Status, x.CreatedUtc });
        b.HasIndex(x => x.BookingId);
    }
}
