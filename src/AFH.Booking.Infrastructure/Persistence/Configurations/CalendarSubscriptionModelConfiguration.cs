using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class CalendarSubscriptionModelConfig : IEntityTypeConfiguration<CalendarSubscriptionModel>
{
    public void Configure(EntityTypeBuilder<CalendarSubscriptionModel> builder)
    {
        builder.ToTable("CalendarSubscriptions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasMaxLength(64);

        builder.Property(x => x.SubscriptionId)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(x => x.SubscriptionId).IsUnique();

        builder.Property(x => x.UserId).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Resource).IsRequired().HasMaxLength(512);
        builder.Property(x => x.NotificationUrl).IsRequired().HasMaxLength(1024);
        builder.Property(x => x.ClientState).IsRequired().HasMaxLength(256);

        builder.Property(x => x.ExpirationUtc).IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate();

        builder.Property(x => x.CreatedUtc).IsRequired();
        builder.Property(x => x.UpdatedUtc).IsRequired();
    }
}