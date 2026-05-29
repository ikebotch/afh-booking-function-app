using AFH.Booking.Application.Abstractions.Notifications;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Models.AdviserProjection;
using AFH.Booking.Application.Models.Lifecycle.Constants;
using AFH.Booking.Application.Models.Notifications;
using AFH.Booking.Application.Services.Lifecycle;
using AFH.Booking.Infrastructure.Notifications;
using AFH.Booking.Infrastructure.Options;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using AFH.Notification.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AFH.Booking.Tests;

public sealed class BookingNotificationPolicyTests
{
    [Fact]
    public async Task Provider_ReturnsDefaultRules_WhenDbRowsAreMissing()
    {
        await using var db = CreateDbContext();
        var provider = new BookingNotificationPolicyProvider(db);

        var policy = await provider.GetAsync("Booking", BookingNotificationTypes.BookingConfirmed, CancellationToken.None);

        Assert.True(policy.Enabled);
        Assert.Contains(policy.Channels, x => x.Channel == NotificationChannel.Email && x.Enabled && x.TemplateKey == "booking-confirmed" && x.TemplateVersion == "v1");
        Assert.Contains(policy.Recipients, x => x.RecipientType == BookingNotificationRecipientTypes.Client && x.Enabled);
    }

    [Fact]
    public async Task Provider_KeepsTemplateVersionPerChannel()
    {
        await using var db = CreateDbContext();
        var ruleId = Guid.NewGuid();
        db.BookingNotificationRules.Add(new BookingNotificationRuleModel
        {
            Id = ruleId,
            SourceApplication = "Booking",
            NotificationType = BookingNotificationTypes.BookingConfirmed.Name,
            Enabled = true,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            Channels =
            [
                new BookingNotificationRuleChannelModel { Id = Guid.NewGuid(), Channel = "Email", Enabled = true, TemplateKey = "booking-confirmed", TemplateVersion = "v2", CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow },
                new BookingNotificationRuleChannelModel { Id = Guid.NewGuid(), Channel = "Sms", Enabled = true, TemplateKey = "booking-confirmed-sms", TemplateVersion = "v1", CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow }
            ]
        });
        await db.SaveChangesAsync();

        var provider = new BookingNotificationPolicyProvider(db);

        var policy = await provider.GetAsync("Booking", BookingNotificationTypes.BookingConfirmed, CancellationToken.None);

        Assert.Equal("v2", policy.GetChannel(NotificationChannel.Email)?.TemplateVersion);
        Assert.Equal("v1", policy.GetChannel(NotificationChannel.Sms)?.TemplateVersion);
        Assert.Equal("booking-confirmed", policy.GetChannel(NotificationChannel.Email)?.TemplateKey);
        Assert.Equal("booking-confirmed-sms", policy.GetChannel(NotificationChannel.Sms)?.TemplateKey);
    }

    [Fact]
    public async Task Provider_AllowsNewRecipientTypeWithoutSchemaChange()
    {
        await using var db = CreateDbContext();
        var ruleId = Guid.NewGuid();
        db.BookingNotificationRules.Add(new BookingNotificationRuleModel
        {
            Id = ruleId,
            SourceApplication = "Booking",
            NotificationType = BookingNotificationTypes.BookingConfirmed.Name,
            Enabled = true,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            Recipients =
            [
                new BookingNotificationRuleRecipientModel { Id = Guid.NewGuid(), RecipientType = "RegionalManager", Enabled = true, CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow }
            ]
        });
        await db.SaveChangesAsync();

        var provider = new BookingNotificationPolicyProvider(db);

        var policy = await provider.GetAsync("Booking", BookingNotificationTypes.BookingConfirmed, CancellationToken.None);

        Assert.Contains(policy.Recipients, x => x.RecipientType == "RegionalManager" && x.Enabled);
    }

    [Fact]
    public async Task Provider_DisablesBookingHoldCreatedByDefault()
    {
        await using var db = CreateDbContext();
        var provider = new BookingNotificationPolicyProvider(db);

        var policy = await provider.GetAsync("Booking", BookingNotificationTypes.BookingHoldCreated, CancellationToken.None);

        Assert.False(policy.Enabled);
        Assert.All(policy.Channels, channel => Assert.False(channel.Enabled));
        Assert.All(policy.Recipients, recipient => Assert.False(recipient.Enabled));
    }

    [Fact]
    public async Task Step_PublishesBookingConfirmedToClientAdviserAndContactCentre_WhenEnabled()
    {
        var publisher = new CapturingPublisher();
        var step = new BookingNotificationStep(
            publisher,
            new StubPolicyProvider(DefaultPolicy(BookingNotificationTypes.BookingConfirmed)),
            CreateRecipientResolver(adviserEmail: "adviser@example.com"),
            NullLogger<BookingNotificationStep>.Instance);

        var result = await step.ExecuteAsync(
            LifecycleEventTypes.Booked,
            "booking-1",
            LifecycleActors.Client,
            [new NotificationRecipient(BookingNotificationRecipientTypes.Client, "Jane Client", "client@example.com")],
            new Dictionary<string, string> { ["bookingId"] = "booking-1", ["adviserId"] = "adv-1", ["adviserName"] = "Ada Adviser" },
            CancellationToken.None);

        Assert.Equal(LifecycleStepStatuses.Succeeded, result.Status);
        Assert.NotNull(publisher.Request);
        Assert.Equal(
            [BookingNotificationRecipientTypes.Adviser, BookingNotificationRecipientTypes.Client, BookingNotificationRecipientTypes.ContactCentre],
            publisher.Request!.Recipients.Select(x => x.RecipientType).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task Step_Skips_WhenChannelIsDisabled()
    {
        var publisher = new CapturingPublisher();
        var policy = DefaultPolicy(BookingNotificationTypes.BookingConfirmed) with
        {
            Channels = [new BookingNotificationChannelPolicy(NotificationChannel.Email, false, "booking-confirmed", "v1")]
        };
        var step = new BookingNotificationStep(
            publisher,
            new StubPolicyProvider(policy),
            CreateRecipientResolver(adviserEmail: "adviser@example.com"),
            NullLogger<BookingNotificationStep>.Instance);

        var result = await step.ExecuteAsync(
            LifecycleEventTypes.Booked,
            "booking-1",
            LifecycleActors.Client,
            [new NotificationRecipient(BookingNotificationRecipientTypes.Client, "Jane Client", "client@example.com")],
            new Dictionary<string, string> { ["bookingId"] = "booking-1", ["adviserId"] = "adv-1" },
            CancellationToken.None);

        Assert.Equal(LifecycleStepStatuses.Skipped, result.Status);
        Assert.Null(publisher.Request);
    }

    [Fact]
    public async Task Resolver_RemovesDisabledRecipient()
    {
        var policy = DefaultPolicy(BookingNotificationTypes.BookingConfirmed) with
        {
            Recipients =
            [
                new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.Client, true),
                new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.Adviser, false),
                new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.ContactCentre, true)
            ]
        };

        var recipients = await CreateRecipientResolver(adviserEmail: "adviser@example.com").ResolveAsync(
            policy,
            [new NotificationRecipient(BookingNotificationRecipientTypes.Client, "Jane Client", "client@example.com")],
            new Dictionary<string, string> { ["adviserId"] = "adv-1" },
            CancellationToken.None);

        Assert.DoesNotContain(recipients, x => x.RecipientType == BookingNotificationRecipientTypes.Adviser);
        Assert.Contains(recipients, x => x.RecipientType == BookingNotificationRecipientTypes.Client);
    }

    [Fact]
    public async Task Resolver_SkipsMissingAdviserEmailOnly()
    {
        var recipients = await CreateRecipientResolver(adviserEmail: null).ResolveAsync(
            DefaultPolicy(BookingNotificationTypes.BookingConfirmed),
            [new NotificationRecipient(BookingNotificationRecipientTypes.Client, "Jane Client", "client@example.com")],
            new Dictionary<string, string> { ["adviserId"] = "adv-1" },
            CancellationToken.None);

        Assert.DoesNotContain(recipients, x => x.RecipientType == BookingNotificationRecipientTypes.Adviser);
        Assert.Contains(recipients, x => x.RecipientType == BookingNotificationRecipientTypes.Client);
        Assert.Contains(recipients, x => x.RecipientType == BookingNotificationRecipientTypes.ContactCentre);
    }

    [Fact]
    public async Task Resolver_DeduplicatesRecipientAddressPerChannel()
    {
        var recipients = await CreateRecipientResolver(adviserEmail: "shared@example.com").ResolveAsync(
            DefaultPolicy(BookingNotificationTypes.BookingConfirmed),
            [new NotificationRecipient(BookingNotificationRecipientTypes.Client, "Jane Client", "shared@example.com")],
            new Dictionary<string, string> { ["adviserId"] = "adv-1" },
            CancellationToken.None);

        var emailTargets = recipients
            .Where(x => x.PreferredChannels?.Contains(NotificationChannel.Email) == true)
            .Select(x => x.Email)
            .Where(x => x is not null)
            .ToArray();

        Assert.Equal(emailTargets.Distinct(StringComparer.OrdinalIgnoreCase).Count(), emailTargets.Length);
    }

    [Fact]
    public async Task Resolver_ContactCentreEmailAddress_ResolvesOneContactCentreRecipient()
    {
        var recipients = await ResolveWithContactCentreOptions(
            contactCentreEmailAddress: "contact@example.com",
            adminBccRecipients: null);

        var contact = Assert.Single(ContactCentreRecipients(recipients));
        Assert.Equal("contact@example.com", contact.Email);
    }

    [Fact]
    public async Task Resolver_AdminBccRecipients_ResolvesMultipleContactCentreRecipients()
    {
        var recipients = await ResolveWithContactCentreOptions(
            contactCentreEmailAddress: null,
            adminBccRecipients: "admin-one@example.com;admin-two@example.com");

        Assert.Equal(
            ["admin-one@example.com", "admin-two@example.com"],
            ContactCentreEmails(recipients));
    }

    [Fact]
    public async Task Resolver_AdminBccRecipients_TakesPrecedenceOverContactCentreEmailAddress()
    {
        var recipients = await ResolveWithContactCentreOptions(
            contactCentreEmailAddress: "contact@example.com",
            adminBccRecipients: "admin@example.com");

        var contact = Assert.Single(ContactCentreRecipients(recipients));
        Assert.Equal("admin@example.com", contact.Email);
    }

    [Fact]
    public async Task Resolver_AdminBccRecipients_SupportsSemicolonAndCommaSeparatedValues()
    {
        var recipients = await ResolveWithContactCentreOptions(
            contactCentreEmailAddress: null,
            adminBccRecipients: "admin-one@example.com; admin-two@example.com,admin-three@example.com");

        Assert.Equal(
            ["admin-one@example.com", "admin-two@example.com", "admin-three@example.com"],
            ContactCentreEmails(recipients));
    }

    [Fact]
    public async Task Resolver_AdminBccRecipients_RemovesDuplicatesCaseInsensitively()
    {
        var recipients = await ResolveWithContactCentreOptions(
            contactCentreEmailAddress: null,
            adminBccRecipients: "Admin@example.com;admin@example.com,other@example.com");

        Assert.Equal(
            ["Admin@example.com", "other@example.com"],
            ContactCentreEmails(recipients));
    }

    [Fact]
    public async Task Resolver_AdminBccRecipients_IgnoresBlankEntries()
    {
        var recipients = await ResolveWithContactCentreOptions(
            contactCentreEmailAddress: null,
            adminBccRecipients: " ; admin@example.com, , ; second@example.com ; ");

        Assert.Equal(
            ["admin@example.com", "second@example.com"],
            ContactCentreEmails(recipients));
    }

    [Fact]
    public async Task Resolver_NoContactCentreEmailConfigured_ReturnsNoContactCentreRecipients()
    {
        var recipients = await ResolveWithContactCentreOptions(
            contactCentreEmailAddress: null,
            adminBccRecipients: null);

        Assert.Empty(ContactCentreRecipients(recipients));
    }

    private static BookingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new BookingDbContext(options);
    }

    private static BookingNotificationPolicy DefaultPolicy(NotificationType type)
        => new(
            "Booking",
            type.Name,
            true,
            [new BookingNotificationChannelPolicy(NotificationChannel.Email, true, "booking-confirmed", "v1")],
            [
                new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.Client, true),
                new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.Adviser, true),
                new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.ContactCentre, true)
            ]);

    private static BookingNotificationRecipientResolver CreateRecipientResolver(
        string? adviserEmail,
        string? contactCentreEmailAddress = "contact@example.com",
        string? adminBccRecipients = null)
    {
        var advisers = new Mock<IAdviserProfileProjectionRepository>();
        advisers.Setup(x => x.GetAsync("adv-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(adviserEmail is null
                ? new AdviserProfileProjectionRecord { AdviserId = "adv-1", DisplayName = "Ada Adviser", MailboxUserId = string.Empty }
                : new AdviserProfileProjectionRecord { AdviserId = "adv-1", DisplayName = "Ada Adviser", MailboxUserId = adviserEmail });

        return new BookingNotificationRecipientResolver(
            advisers.Object,
            Options.Create(new NotificationEmailOptions
            {
                ContactCentreEmailAddress = contactCentreEmailAddress,
                AdminBccRecipients = adminBccRecipients
            }),
            NullLogger<BookingNotificationRecipientResolver>.Instance);
    }

    private static async Task<IReadOnlyList<NotificationRecipient>> ResolveWithContactCentreOptions(
        string? contactCentreEmailAddress,
        string? adminBccRecipients)
    {
        return await CreateRecipientResolver(
                adviserEmail: null,
                contactCentreEmailAddress,
                adminBccRecipients)
            .ResolveAsync(
                DefaultPolicy(BookingNotificationTypes.BookingConfirmed),
                [new NotificationRecipient(BookingNotificationRecipientTypes.Client, "Jane Client", "client@example.com")],
                new Dictionary<string, string> { ["adviserId"] = "adv-1" },
                CancellationToken.None);
    }

    private static NotificationRecipient[] ContactCentreRecipients(IReadOnlyList<NotificationRecipient> recipients)
        => recipients
            .Where(x => x.RecipientType == BookingNotificationRecipientTypes.ContactCentre)
            .ToArray();

    private static string[] ContactCentreEmails(IReadOnlyList<NotificationRecipient> recipients)
        => ContactCentreRecipients(recipients)
            .Select(x => x.Email)
            .OfType<string>()
            .ToArray();

    private sealed class StubPolicyProvider(BookingNotificationPolicy policy) : IBookingNotificationPolicyProvider
    {
        public Task<BookingNotificationPolicy> GetAsync(string sourceApplication, NotificationType notificationType, CancellationToken ct)
            => Task.FromResult(policy);
    }

    private sealed class CapturingPublisher : INotificationPublisher
    {
        public NotificationRequested? Request { get; private set; }

        public Task PublishAsync(NotificationRequested notification, CancellationToken ct)
        {
            Request = notification;
            return Task.CompletedTask;
        }
    }
}