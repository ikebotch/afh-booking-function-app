using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class PartnerWorkflowRuleModelConfiguration : IEntityTypeConfiguration<PartnerWorkflowRuleModel>
{
    public void Configure(EntityTypeBuilder<PartnerWorkflowRuleModel> b)
    {
        b.ToTable("PartnerWorkflowRules");
        b.HasKey(x => new { x.ChangeType, x.PartnerKey });
        b.Property(x => x.ChangeType).HasMaxLength(64).IsRequired();
        b.Property(x => x.PartnerKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.Enabled).IsRequired();
        b.Property(x => x.CreatedUtc).IsRequired();
        b.Property(x => x.UpdatedUtc).IsRequired();

        b.HasOne(x => x.Endpoint)
            .WithMany()
            .HasForeignKey(x => x.PartnerKey)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
