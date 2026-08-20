using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class PartnerWorkflowEndpointModelConfiguration : IEntityTypeConfiguration<PartnerWorkflowEndpointModel>
{
    public void Configure(EntityTypeBuilder<PartnerWorkflowEndpointModel> b)
    {
        b.ToTable("PartnerWorkflowEndpoints");
        b.HasKey(x => x.PartnerKey);
        b.Property(x => x.PartnerKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Enabled).IsRequired();
        b.Property(x => x.BookingUpdatesUrl).HasMaxLength(2048);
        b.Property(x => x.BaseUrl).HasMaxLength(2048);
        b.Property(x => x.BookingUpdatesPath).HasMaxLength(512).IsRequired();
        b.Property(x => x.ApiKey).HasMaxLength(2048);
        b.Property(x => x.ApiKeyHeaderName).HasMaxLength(128).IsRequired();
        b.Property(x => x.IdempotencyKeyHeaderName).HasMaxLength(128).IsRequired();
        b.Property(x => x.PayloadFormat).HasMaxLength(64).IsRequired();
        b.Property(x => x.CreatedUtc).IsRequired();
        b.Property(x => x.UpdatedUtc).IsRequired();
    }
}
