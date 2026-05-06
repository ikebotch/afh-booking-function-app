using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class MeetingTypeModelConfiguration : IEntityTypeConfiguration<MeetingTypeModel>
{
    public void Configure(EntityTypeBuilder<MeetingTypeModel> b)
    {
        b.ToTable("MeetingTypes");
        b.HasKey(x => x.Code);

        b.Property(x => x.Code).HasMaxLength(128).IsRequired();
        b.Property(x => x.Label).HasMaxLength(256).IsRequired();
        b.Property(x => x.IsDefault).IsRequired();
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.DefaultDurationMinutes);
        b.Property(x => x.SortOrder).IsRequired();
        b.Property(x => x.CreatedUtc).IsRequired();
        b.Property(x => x.UpdatedUtc);

        b.HasIndex(x => new { x.IsActive, x.SortOrder });
    }
}
