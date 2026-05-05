using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class LifecycleEventModelConfiguration : IEntityTypeConfiguration<LifecycleEventModel>
{
    public void Configure(EntityTypeBuilder<LifecycleEventModel> b)
    {
        b.ToTable("LifecycleEvents");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasMaxLength(64).IsRequired();
        b.Property(x => x.BookingId).HasMaxLength(64).IsRequired();
        b.Property(x => x.TransactionId).HasMaxLength(64);
        b.Property(x => x.EventType).HasMaxLength(128).IsRequired();
        b.Property(x => x.PreviousState).HasMaxLength(64);
        b.Property(x => x.NewState).HasMaxLength(64);
        b.Property(x => x.ActorType).HasMaxLength(64);
        b.Property(x => x.ActorId).HasMaxLength(128);
        b.Property(x => x.ReasonCode).HasMaxLength(128);
        b.Property(x => x.ReasonNotes).HasMaxLength(2048);
        b.Property(x => x.BeforeJson).HasMaxLength(4000);
        b.Property(x => x.AfterJson).HasMaxLength(4000);
        b.Property(x => x.CorrelationId).HasMaxLength(128);
        b.Property(x => x.SourceSystem).HasMaxLength(64);
        b.Property(x => x.RelatedBookingId).HasMaxLength(64);

        b.HasMany(x => x.Steps)
            .WithOne(x => x.LifecycleEvent)
            .HasForeignKey(x => x.LifecycleEventId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.BookingId);
        b.HasIndex(x => x.TransactionId);
        b.HasIndex(x => x.EventType);
        b.HasIndex(x => x.NewState);
        b.HasIndex(x => x.OccurredUtc);
        b.HasIndex(x => x.CorrelationId);
    }
}
