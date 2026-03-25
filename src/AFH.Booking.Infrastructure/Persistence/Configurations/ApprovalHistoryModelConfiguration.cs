using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class ApprovalHistoryModelConfiguration : IEntityTypeConfiguration<ApprovalHistoryModel>
{
    public void Configure(EntityTypeBuilder<ApprovalHistoryModel> b)
    {
        b.ToTable("ApprovalHistory");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasMaxLength(64).IsRequired();
        b.Property(x => x.ApprovalRequestId).HasMaxLength(64).IsRequired();
        b.Property(x => x.EventType).HasMaxLength(64).IsRequired();
        b.Property(x => x.ActorType).HasMaxLength(32).IsRequired();
        b.Property(x => x.ActorId).HasMaxLength(128);
        b.Property(x => x.Outcome).HasMaxLength(32).IsRequired();
        b.Property(x => x.Comments).HasMaxLength(1024);

        b.HasIndex(x => x.ApprovalRequestId);
        b.HasIndex(x => x.OccurredUtc);
    }
}
