using AFH.Booking.Application.Models.Notifications;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class NotificationRuleModelConfiguration : IEntityTypeConfiguration<NotificationRuleModel>
{
    private static readonly DateTime SeededUtc = new(2026, 5, 28, 0, 0, 0, DateTimeKind.Utc);

    private static readonly Guid BookingConfirmedRuleId = Guid.Parse("4a6f3c68-4eb7-4788-bd13-6908b33c7951");
    private static readonly Guid BookingRescheduledRuleId = Guid.Parse("7f6c8f21-9d36-481e-b5ad-08454d0036a6");
    private static readonly Guid BookingCancelledRuleId = Guid.Parse("f6096d32-d3c7-49d4-8d16-9b7135e66274");
    private static readonly Guid BookingHoldCreatedRuleId = Guid.Parse("4e428654-c797-4f91-97fa-bd7b5086395a");
    private static readonly Guid AdviserRequestSubmittedRuleId = Guid.Parse("19c2f130-6649-42ed-82cb-95f0d8706d01");
    private static readonly Guid AdviserRequestOutcomeRuleId = Guid.Parse("9b9be0be-7633-4576-ba6d-24ef060a0e6d");

    public void Configure(EntityTypeBuilder<NotificationRuleModel> b)
    {
        b.ToTable("NotificationRules");
        b.HasKey(x => x.Id);

        b.Property(x => x.SourceApplication).HasMaxLength(100).IsRequired();
        b.Property(x => x.NotificationType).HasMaxLength(150).IsRequired();
        b.Property(x => x.Enabled).IsRequired();
        b.Property(x => x.CreatedUtc).IsRequired();
        b.Property(x => x.UpdatedUtc).IsRequired();

        b.HasIndex(x => new { x.SourceApplication, x.NotificationType }).IsUnique();

        b.HasMany(x => x.Channels)
            .WithOne(x => x.Rule)
            .HasForeignKey(x => x.RuleId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Recipients)
            .WithOne(x => x.Rule)
            .HasForeignKey(x => x.RuleId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasData(
            Rule(BookingConfirmedRuleId, BookingNotificationTypes.BookingConfirmed.Name, enabled: true),
            Rule(BookingRescheduledRuleId, BookingNotificationTypes.BookingRescheduled.Name, enabled: true),
            Rule(BookingCancelledRuleId, BookingNotificationTypes.BookingCancelled.Name, enabled: true),
            Rule(BookingHoldCreatedRuleId, BookingNotificationTypes.BookingHoldCreated.Name, enabled: false),
            Rule(AdviserRequestSubmittedRuleId, BookingNotificationTypes.AdviserRequestSubmitted.Name, enabled: true),
            Rule(AdviserRequestOutcomeRuleId, BookingNotificationTypes.AdviserRequestOutcome.Name, enabled: true));
    }

    internal static IReadOnlyDictionary<string, Guid> RuleIds => new Dictionary<string, Guid>(StringComparer.Ordinal)
    {
        [BookingNotificationTypes.BookingConfirmed.Name] = BookingConfirmedRuleId,
        [BookingNotificationTypes.BookingRescheduled.Name] = BookingRescheduledRuleId,
        [BookingNotificationTypes.BookingCancelled.Name] = BookingCancelledRuleId,
        [BookingNotificationTypes.BookingHoldCreated.Name] = BookingHoldCreatedRuleId,
        [BookingNotificationTypes.AdviserRequestSubmitted.Name] = AdviserRequestSubmittedRuleId,
        [BookingNotificationTypes.AdviserRequestOutcome.Name] = AdviserRequestOutcomeRuleId
    };

    internal static DateTime SeedTimestampUtc => SeededUtc;

    private static NotificationRuleModel Rule(Guid id, string notificationType, bool enabled)
        => new()
        {
            Id = id,
            SourceApplication = "Booking",
            NotificationType = notificationType,
            Enabled = enabled,
            CreatedUtc = SeededUtc,
            UpdatedUtc = SeededUtc
        };
}
