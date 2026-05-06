using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class MeetingTopicModelConfiguration : IEntityTypeConfiguration<MeetingTopicModel>
{
    public void Configure(EntityTypeBuilder<MeetingTopicModel> b)
    {
        b.ToTable("MeetingTopics");
        b.HasKey(x => x.Code);

        b.Property(x => x.Code).HasMaxLength(128).IsRequired();
        b.Property(x => x.Label).HasMaxLength(256).IsRequired();
        b.Property(x => x.IsDefault).IsRequired();
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.SortOrder).IsRequired();
        b.Property(x => x.CreatedUtc).IsRequired();
        b.Property(x => x.UpdatedUtc);

        b.HasIndex(x => new { x.IsActive, x.SortOrder });
    }
}
