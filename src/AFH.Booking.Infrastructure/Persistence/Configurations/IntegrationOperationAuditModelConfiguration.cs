using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class IntegrationOperationAuditModelConfiguration : IEntityTypeConfiguration<IntegrationOperationAuditModel>
{
    public void Configure(EntityTypeBuilder<IntegrationOperationAuditModel> b)
    {
        b.ToTable("IntegrationOperationAudit");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.ServiceName).HasMaxLength(64).IsRequired();
        b.Property(x => x.FunctionName).HasMaxLength(256).IsRequired();
        b.Property(x => x.Method).HasMaxLength(16).IsRequired();
        b.Property(x => x.Path).HasMaxLength(512).IsRequired();
        b.Property(x => x.QueryString).HasMaxLength(2048);
        b.Property(x => x.CorrelationId).HasMaxLength(128);
        b.Property(x => x.OperationId).HasMaxLength(128).IsRequired();
        b.Property(x => x.StatusCode).IsRequired();
        b.Property(x => x.DurationMs).IsRequired();
        b.Property(x => x.ErrorType).HasMaxLength(128);
        b.Property(x => x.ErrorMessage).HasMaxLength(2048);
        b.Property(x => x.CreatedUtc).IsRequired();

        b.HasIndex(x => x.CreatedUtc);
        b.HasIndex(x => x.CorrelationId);
        b.HasIndex(x => new { x.FunctionName, x.CreatedUtc });
    }
}
