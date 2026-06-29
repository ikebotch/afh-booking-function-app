using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class ApprovalRequestReferenceAllocationModelConfiguration : IEntityTypeConfiguration<ApprovalRequestReferenceAllocationModel>
{
    public void Configure(EntityTypeBuilder<ApprovalRequestReferenceAllocationModel> b)
    {
        b.ToTable("ApprovalRequestReferenceAllocations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64).IsRequired();
        b.Property(x => x.Value)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEXT VALUE FOR dbo.ApprovalRequestReferenceNumber");
        b.Property(x => x.CreatedUtc).IsRequired();
        b.HasIndex(x => x.CreatedUtc);
    }
}
