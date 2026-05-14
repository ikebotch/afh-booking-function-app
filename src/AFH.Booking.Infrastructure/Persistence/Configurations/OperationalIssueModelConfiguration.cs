using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class OperationalIssueModelConfiguration : IEntityTypeConfiguration<OperationalIssueModel>
{
    public void Configure(EntityTypeBuilder<OperationalIssueModel> b)
    {
        b.ToTable("OperationalIssues");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.IssueType).HasMaxLength(64).IsRequired();
        b.Property(x => x.Code).HasMaxLength(128).IsRequired();
        b.Property(x => x.Severity).HasMaxLength(32).IsRequired();
        b.Property(x => x.Status).HasMaxLength(64).IsRequired();
        b.Property(x => x.BookingId).HasMaxLength(64);
        b.Property(x => x.TransactionId).HasMaxLength(64);
        b.Property(x => x.TransactionRef).HasMaxLength(128);
        b.Property(x => x.AdviserId).HasMaxLength(256);
        b.Property(x => x.ProviderEventId).HasMaxLength(512);
        b.Property(x => x.CorrelationId).HasMaxLength(128);
        b.Property(x => x.MetadataJson).HasMaxLength(4000);

        b.HasIndex(x => new { x.AdviserId, x.Code, x.DetectedUtc });
        b.HasIndex(x => new { x.ProviderEventId, x.Code, x.DetectedUtc });
    }
}
