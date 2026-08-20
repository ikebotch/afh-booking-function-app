using AFH.Booking.Application.Models.Notifications;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class BookingNotificationRuleChannelModelConfiguration : IEntityTypeConfiguration<BookingNotificationRuleChannelModel>
{
    public void Configure(EntityTypeBuilder<BookingNotificationRuleChannelModel> b)
    {
        b.ToTable("BookingNotificationRuleChannels");
        b.HasKey(x => x.Id);

        b.Property(x => x.Channel).HasMaxLength(50).IsRequired();
        b.Property(x => x.Enabled).IsRequired();
        b.Property(x => x.TemplateKey).HasMaxLength(150).IsRequired();
        b.Property(x => x.TemplateVersion).HasMaxLength(50).IsRequired();
        b.Property(x => x.CreatedUtc).IsRequired();
        b.Property(x => x.UpdatedUtc).IsRequired();

        b.HasIndex(x => new { x.RuleId, x.Channel }).IsUnique();

        b.HasData(CreateSeeds());
    }

    private static IReadOnlyList<BookingNotificationRuleChannelModel> CreateSeeds()
    {
        var now = BookingNotificationRuleModelConfiguration.SeedTimestampUtc;
        return
        [
            Channel("728736f1-89e2-4315-bab4-4670b526a201", "BookingConfirmed", "Email", true, "booking-confirmed", "v1", now),
            Channel("77fe97da-c2c5-44fb-8394-67e2f89d04b7", "BookingRescheduled", "Email", true, "booking-rescheduled", "v1", now),
            Channel("9cb40ca9-95c0-4fd2-9d87-51e711164a7b", "BookingCancelled", "Email", true, "booking-cancelled", "v1", now),
            Channel("04eec961-a8f0-4cc9-bec7-d5c28c3356af", "BookingHoldCreated", "Email", false, "booking-hold", "v1", now),
            Channel("14179cdb-d3a0-4142-80a0-387403fd9201", "AdviserRequestSubmitted", "Email", true, "adviser-request-submitted", "v1", now),
            Channel("9beec961-a8f0-4cc9-bec7-d5c28c3357af", "AdviserRequestOutcome", "Email", true, "adviser-request-outcome", "v1", now),
            Channel("6e093647-7ca9-4f98-9110-352533da95ef", "BookingConfirmed", "Sms", false, "booking-confirmed-sms", "v1", now),
            Channel("d5a7d12e-d0a9-40a3-8911-841cd5753c1e", "BookingRescheduled", "Sms", false, "booking-rescheduled-sms", "v1", now),
            Channel("b2b3e321-e4e0-4223-a840-b262bfe6ef41", "BookingCancelled", "Sms", false, "booking-cancelled-sms", "v1", now),
            Channel("aa463227-5113-4c56-881e-07d47085c18e", "BookingHoldCreated", "Sms", false, "booking-hold-sms", "v1", now),
            Channel("6af1c6ad-c7c2-4d98-8859-c36f9f4d9202", "AdviserRequestSubmitted", "Sms", false, "adviser-request-submitted-sms", "v1", now),
            Channel("9beec961-a8f0-4cc9-bec7-d5c28c3333bf", "AdviserRequestOutcome", "Sms", false, "adviser-request-outcome-sms", "v1", now)
        ];
    }

    private static BookingNotificationRuleChannelModel Channel(
        string id,
        string notificationType,
        string channel,
        bool enabled,
        string templateKey,
        string templateVersion,
        DateTime now)
        => new()
        {
            Id = Guid.Parse(id),
            RuleId = BookingNotificationRuleModelConfiguration.RuleIds[notificationType],
            Channel = channel,
            Enabled = enabled,
            TemplateKey = templateKey,
            TemplateVersion = templateVersion,
            CreatedUtc = now,
            UpdatedUtc = now
        };
}
