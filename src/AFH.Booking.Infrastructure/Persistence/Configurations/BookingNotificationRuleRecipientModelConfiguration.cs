using AFH.Booking.Application.Models.Notifications;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class BookingNotificationRuleRecipientModelConfiguration : IEntityTypeConfiguration<BookingNotificationRuleRecipientModel>
{
    public void Configure(EntityTypeBuilder<BookingNotificationRuleRecipientModel> b)
    {
        b.ToTable("BookingNotificationRuleRecipients");
        b.HasKey(x => x.Id);

        b.Property(x => x.RecipientType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Enabled).IsRequired();
        b.Property(x => x.CreatedUtc).IsRequired();
        b.Property(x => x.UpdatedUtc).IsRequired();

        b.HasIndex(x => new { x.RuleId, x.RecipientType }).IsUnique();

        b.HasData(CreateSeeds());
    }

    private static IReadOnlyList<BookingNotificationRuleRecipientModel> CreateSeeds()
    {
        var rows = new List<BookingNotificationRuleRecipientModel>();
        var now = BookingNotificationRuleModelConfiguration.SeedTimestampUtc;
        Add(rows, "BookingConfirmed", enabled: true, now, "f3096f1b-fd20-4cc3-a431-22f1d1021d01", "12d8a812-96fc-4020-93cd-30a82efa3c02", "8f7c2b57-2d84-4d52-b9d0-00f1c9ccde01", "f7726ed6-0939-49d3-b18c-5f2e34959f03");
        Add(rows, "BookingRescheduled", enabled: true, now, "982657c1-2041-4fed-9cb1-5a802e258704", "42cda070-c4cd-4fdb-ab79-c3d6870baf7e", "10f4906d-1d29-41de-8baa-aecf96c5de02", "55f92361-2f74-4128-a794-703eff8ca9e3");
        Add(rows, "BookingCancelled", enabled: true, now, "a689b522-d031-44db-b145-e8109350c271", "566301d9-a5d6-4e2d-a688-65b6d11c00f2", "9224f2f1-8408-423e-a318-cb3dd69fde03", "27650cba-e5cf-4777-903d-24123832d1a3");
        Add(rows, "BookingHoldCreated", enabled: false, now, "9c987676-c344-476a-a57a-8a22e2d97922", "6bc808c6-53a3-473f-972d-d8871678e4d4", "d39f9f03-df6f-4a1e-b7cb-dc94b2ddde04", "84186f63-a40d-419b-933d-573106880aeb");
        return rows;
    }

    private static void Add(
        List<BookingNotificationRuleRecipientModel> rows,
        string notificationType,
        bool enabled,
        DateTime now,
        string clientId,
        string adviserId,
        string managerId,
        string contactCentreId)
    {
        rows.Add(Recipient(clientId, notificationType, BookingNotificationRecipientTypes.Client, enabled, now));
        rows.Add(Recipient(adviserId, notificationType, BookingNotificationRecipientTypes.Adviser, enabled, now));
        rows.Add(Recipient(managerId, notificationType, BookingNotificationRecipientTypes.Manager, enabled, now));
        rows.Add(Recipient(contactCentreId, notificationType, BookingNotificationRecipientTypes.ContactCentre, enabled, now));
    }

    private static BookingNotificationRuleRecipientModel Recipient(
        string id,
        string notificationType,
        string recipientType,
        bool enabled,
        DateTime now)
        => new()
        {
            Id = Guid.Parse(id),
            RuleId = BookingNotificationRuleModelConfiguration.RuleIds[notificationType],
            RecipientType = recipientType,
            Enabled = enabled,
            CreatedUtc = now,
            UpdatedUtc = now
        };
}
