using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class ApprovalRequestModelConfiguration : IEntityTypeConfiguration<ApprovalRequestModel>
{
    public void Configure(EntityTypeBuilder<ApprovalRequestModel> b)
    {
        b.ToTable("ApprovalRequests");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasMaxLength(64).IsRequired();
        b.Property(x => x.BookingId).HasMaxLength(64).IsRequired();
        b.Property(x => x.ChangeType).HasMaxLength(32).IsRequired();
        b.Property(x => x.RequestedBy).HasMaxLength(32).IsRequired();
        b.Property(x => x.Status).HasMaxLength(32).IsRequired();
        b.Property(x => x.ReasonCode).HasMaxLength(128);
        b.Property(x => x.ReasonDetail).HasMaxLength(1024);
        b.Property(x => x.Reviewer).HasMaxLength(128);
        b.Property(x => x.ReviewNotes).HasMaxLength(1024);

        b.HasIndex(x => x.BookingId);
        b.HasIndex(x => new { x.Status, x.RequestedUtc });
    }
}
