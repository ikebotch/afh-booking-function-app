using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class LifecycleStepModelConfiguration : IEntityTypeConfiguration<LifecycleStepModel>
{
    public void Configure(EntityTypeBuilder<LifecycleStepModel> b)
    {
        b.ToTable("LifecycleSteps");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasMaxLength(64).IsRequired();
        b.Property(x => x.LifecycleEventId).HasMaxLength(64).IsRequired();
        b.Property(x => x.StepName).HasMaxLength(64).IsRequired();
        b.Property(x => x.Status).HasMaxLength(32).IsRequired();
        b.Property(x => x.ErrorCode).HasMaxLength(128);
        b.Property(x => x.ErrorDetails).HasMaxLength(2048);
        b.Property(x => x.CorrelationId).HasMaxLength(128);

        b.HasIndex(x => x.LifecycleEventId);
        b.HasIndex(x => new { x.LifecycleEventId, x.Sequence }).IsUnique();
    }
}
