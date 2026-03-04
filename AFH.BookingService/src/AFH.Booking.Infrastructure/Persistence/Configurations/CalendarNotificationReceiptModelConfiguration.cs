using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class CalendarNotificationReceiptModelConfiguration : IEntityTypeConfiguration<CalendarNotificationReceiptModel>
{
    public void Configure(EntityTypeBuilder<CalendarNotificationReceiptModel> b)
    {
        b.ToTable("CalendarNotificationReceipts");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasMaxLength(64);

        b.Property(x => x.SubscriptionId).IsRequired().HasMaxLength(256);
        b.Property(x => x.EventId).IsRequired().HasMaxLength(512);
        b.Property(x => x.ChangeType).HasMaxLength(50);
        b.Property(x => x.ClientState).HasMaxLength(256);

        b.Property(x => x.ReceivedUtc).IsRequired();

        b.Property(x => x.RawPayload).HasMaxLength(4000); 
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => new { x.SubscriptionId, x.EventId, x.ReceivedUtc });





        b.Property(x => x.Accepted).IsRequired();
        b.Property(x => x.RejectReason).HasMaxLength(256);
        b.HasIndex(x => new { x.Accepted, x.ReceivedUtc });
    }
}