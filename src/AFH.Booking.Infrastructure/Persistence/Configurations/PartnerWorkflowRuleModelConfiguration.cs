using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class PartnerWorkflowRuleModelConfiguration : IEntityTypeConfiguration<PartnerWorkflowRuleModel>
{
    public void Configure(EntityTypeBuilder<PartnerWorkflowRuleModel> b)
    {
        b.ToTable("PartnerWorkflowRules");
        b.HasKey(x => x.ChangeType);
        b.Property(x => x.ChangeType).HasMaxLength(64).IsRequired();
        b.Property(x => x.Enabled).IsRequired();
        b.Property(x => x.CreatedUtc).IsRequired();
        b.Property(x => x.UpdatedUtc).IsRequired();
    }
}
