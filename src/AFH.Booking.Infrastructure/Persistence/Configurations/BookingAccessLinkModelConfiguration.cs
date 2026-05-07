using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class BookingAccessLinkModelConfiguration : IEntityTypeConfiguration<BookingAccessLinkModel>
{
    public void Configure(EntityTypeBuilder<BookingAccessLinkModel> b)
    {
        b.ToTable("BookingAccessLinks");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasMaxLength(64).IsRequired();
        b.Property(x => x.OriginalBookingId).HasMaxLength(64).IsRequired();
        b.Property(x => x.CurrentBookingId).HasMaxLength(64).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        b.Property(x => x.ActorType).HasMaxLength(64).IsRequired();
        b.Property(x => x.ActorId).HasMaxLength(128);
        b.Property(x => x.TransactionRef).HasMaxLength(128);
        b.Property(x => x.ExpiresUtc).IsRequired();
        b.Property(x => x.CreatedUtc).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(128);
        b.Property(x => x.RevokedReason).HasMaxLength(256);

        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.OriginalBookingId);
        b.HasIndex(x => x.CurrentBookingId);
        b.HasIndex(x => new { x.CurrentBookingId, x.RevokedUtc, x.ExpiresUtc });
    }
}
