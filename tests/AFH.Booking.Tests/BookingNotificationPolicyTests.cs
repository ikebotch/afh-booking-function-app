using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Notifications;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Models.AdviserProjection;
using AFH.Booking.Application.Models.BusinessContacts;
using AFH.Booking.Application.Models.Lifecycle.Constants;
using AFH.Booking.Application.Models.Notifications;
using AFH.Booking.Application.Services.Lifecycle;
using AFH.Booking.Infrastructure.Notifications;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
        Assert.Contains(policy.Channels, x => x.Channel == BookingNotificationChannel.Email && x.Enabled && x.TemplateKey == "booking-confirmed" && x.TemplateVersion == "v1");
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

        Assert.Equal("v2", policy.GetChannel(BookingNotificationChannel.Email)?.TemplateVersion);
        Assert.Equal("v1", policy.GetChannel(BookingNotificationChannel.Sms)?.TemplateVersion);
        Assert.Equal("booking-confirmed", policy.GetChannel(BookingNotificationChannel.Email)?.TemplateKey);
        Assert.Equal("booking-confirmed-sms", policy.GetChannel(BookingNotificationChannel.Sms)?.TemplateKey);
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
            [new BookingNotificationRecipient(BookingNotificationRecipientTypes.Client, "Jane Client", "client@example.com")],
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
            Channels = [new BookingNotificationChannelPolicy(BookingNotificationChannel.Email, false, "booking-confirmed", "v1")]
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
            [new BookingNotificationRecipient(BookingNotificationRecipientTypes.Client, "Jane Client", "client@example.com")],
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
            [new BookingNotificationRecipient(BookingNotificationRecipientTypes.Client, "Jane Client", "client@example.com")],
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
            [new BookingNotificationRecipient(BookingNotificationRecipientTypes.Client, "Jane Client", "client@example.com")],
            new Dictionary<string, string> { ["adviserId"] = "adv-1" },
            CancellationToken.None);

        Assert.DoesNotContain(recipients, x => x.RecipientType == BookingNotificationRecipientTypes.Adviser);
        Assert.Contains(recipients, x => x.RecipientType == BookingNotificationRecipientTypes.Client);
        Assert.Contains(recipients, x => x.RecipientType == BookingNotificationRecipientTypes.ContactCentre);
    }

    [Fact]
    public async Task Resolver_CallsBusinessContactsEndpointWithNeutralRoles()
    {
        var contacts = new StubBusinessContactsClient(
            new BookingBusinessContact(
                BookingNotificationRecipientTypes.ContactCentre,
                "Contact Centre",
                "contact@example.com",
                null,
                [BookingNotificationChannel.Email]));

        await CreateRecipientResolver(adviserEmail: null, businessContacts: contacts).ResolveAsync(
            DefaultPolicy(BookingNotificationTypes.BookingConfirmed),
            [new BookingNotificationRecipient(BookingNotificationRecipientTypes.Client, "Jane Client", "client@example.com")],
            new Dictionary<string, string>
            {
                ["bookingId"] = "booking-1",
                ["adviserId"] = "adv-1",
                ["region"] = "South",
                ["organisationId"] = "org-1",
                ["clientId"] = "client-1"
            },
            CancellationToken.None);

        Assert.NotNull(contacts.LastSearch);
        Assert.Equal([BookingNotificationRecipientTypes.ContactCentre], contacts.LastSearch!.ContactTypes);
        Assert.Equal("adv-1", contacts.LastSearch.AdviserId);
        Assert.Equal("South", contacts.LastSearch.Region);
        Assert.Equal("org-1", contacts.LastSearch.OrganisationId);
        Assert.Equal("client-1", contacts.LastSearch.ClientId);
    }

    [Fact]
    public async Task Resolver_MapsBusinessContactsIntoNotificationRecipients()
    {
        var recipients = await CreateRecipientResolver(
                adviserEmail: null,
                businessContacts: new StubBusinessContactsClient(
                    new BookingBusinessContact(
                        BookingNotificationRecipientTypes.ContactCentre,
                        "Contact Centre",
                        "contact@example.com",
                        null,
                        [BookingNotificationChannel.Email])))
            .ResolveAsync(
                DefaultPolicy(BookingNotificationTypes.BookingConfirmed),
                [new BookingNotificationRecipient(BookingNotificationRecipientTypes.Client, "Jane Client", "client@example.com")],
                new Dictionary<string, string> { ["bookingId"] = "booking-1", ["adviserId"] = "adv-1" },
                CancellationToken.None);

        var contact = Assert.Single(ContactCentreRecipients(recipients));
        Assert.Equal("Contact Centre", contact.DisplayName);
        Assert.Equal("contact@example.com", contact.Email);
        Assert.Equal([BookingNotificationChannel.Email], contact.PreferredChannels);
    }

    [Fact]
    public async Task Resolver_MissingBusinessContactRole_SkipsOnlyMissingRole()
    {
        var recipients = await CreateRecipientResolver(
                adviserEmail: "adviser@example.com",
                businessContacts: new StubBusinessContactsClient())
            .ResolveAsync(
                DefaultPolicy(BookingNotificationTypes.BookingConfirmed),
                [new BookingNotificationRecipient(BookingNotificationRecipientTypes.Client, "Jane Client", "client@example.com")],
                new Dictionary<string, string> { ["bookingId"] = "booking-1", ["adviserId"] = "adv-1" },
                CancellationToken.None);

        Assert.Contains(recipients, x => x.RecipientType == BookingNotificationRecipientTypes.Client);
        Assert.Contains(recipients, x => x.RecipientType == BookingNotificationRecipientTypes.Adviser);
        Assert.DoesNotContain(recipients, x => x.RecipientType == BookingNotificationRecipientTypes.ContactCentre);
    }

    [Fact]
    public async Task Resolver_DeduplicatesRecipientAddressPerChannel()
    {
        var recipients = await CreateRecipientResolver(adviserEmail: "shared@example.com").ResolveAsync(
            DefaultPolicy(BookingNotificationTypes.BookingConfirmed),
            [new BookingNotificationRecipient(BookingNotificationRecipientTypes.Client, "Jane Client", "shared@example.com")],
            new Dictionary<string, string> { ["adviserId"] = "adv-1" },
            CancellationToken.None);

        var emailTargets = recipients
            .Where(x => x.PreferredChannels?.Contains(BookingNotificationChannel.Email) == true)
            .Select(x => x.Email)
            .Where(x => x is not null)
            .ToArray();

        Assert.Equal(emailTargets.Distinct(StringComparer.OrdinalIgnoreCase).Count(), emailTargets.Length);
    }

    [Fact]
    public async Task Step_Skips_WhenNoRecipientsRemain()
    {
        var publisher = new CapturingPublisher();
        var policy = DefaultPolicy(BookingNotificationTypes.BookingConfirmed) with
        {
            Recipients = [new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.ContactCentre, true)]
        };
        var step = new BookingNotificationStep(
            publisher,
            new StubPolicyProvider(policy),
            CreateRecipientResolver(adviserEmail: null, businessContacts: new StubBusinessContactsClient()),
            NullLogger<BookingNotificationStep>.Instance);

        var result = await step.ExecuteAsync(
            LifecycleEventTypes.Booked,
            "booking-1",
            LifecycleActors.Client,
            [],
            new Dictionary<string, string> { ["bookingId"] = "booking-1" },
            CancellationToken.None);

        Assert.Equal(LifecycleStepStatuses.Skipped, result.Status);
        Assert.Null(publisher.Request);
    }

    private static BookingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new BookingDbContext(options);
    }

    private static BookingNotificationPolicy DefaultPolicy(BookingNotificationType type)
        => new(
            "Booking",
            type.Name,
            true,
            [new BookingNotificationChannelPolicy(BookingNotificationChannel.Email, true, "booking-confirmed", "v1")],
            [
                new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.Client, true),
                new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.Adviser, true),
                new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.ContactCentre, true)
            ]);

    private static BookingNotificationRecipientResolver CreateRecipientResolver(
        string? adviserEmail,
        StubBusinessContactsClient? businessContacts = null)
    {
        var advisers = new Mock<IAdviserProfileProjectionRepository>();
        advisers.Setup(x => x.GetAsync("adv-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(adviserEmail is null
                ? new AdviserProfileProjectionRecord { AdviserId = "adv-1", DisplayName = "Ada Adviser", MailboxUserId = string.Empty }
                : new AdviserProfileProjectionRecord { AdviserId = "adv-1", DisplayName = "Ada Adviser", MailboxUserId = adviserEmail });

        return new BookingNotificationRecipientResolver(
            advisers.Object,
            businessContacts ?? new StubBusinessContactsClient(
                new BookingBusinessContact(
                    BookingNotificationRecipientTypes.ContactCentre,
                    "Contact Centre",
                    "contact@example.com",
                    null,
                    [BookingNotificationChannel.Email])),
            NullLogger<BookingNotificationRecipientResolver>.Instance);
    }

    private static BookingNotificationRecipient[] ContactCentreRecipients(IReadOnlyList<BookingNotificationRecipient> recipients)
        => recipients
            .Where(x => x.RecipientType == BookingNotificationRecipientTypes.ContactCentre)
            .ToArray();

    private static string[] ContactCentreEmails(IReadOnlyList<BookingNotificationRecipient> recipients)
        => ContactCentreRecipients(recipients)
            .Select(x => x.Email)
            .OfType<string>()
            .ToArray();

    private sealed class StubPolicyProvider(BookingNotificationPolicy policy) : IBookingNotificationPolicyProvider
    {
        public Task<BookingNotificationPolicy> GetAsync(string sourceApplication, BookingNotificationType notificationType, CancellationToken ct)
            => Task.FromResult(policy);
    }

    private sealed class CapturingPublisher : IBookingNotificationPublisher
    {
        public BookingNotificationRequest? Request { get; private set; }

        public Task PublishAsync(BookingNotificationRequest notification, CancellationToken ct)
        {
            Request = notification;
            return Task.CompletedTask;
        }
    }

    private sealed class StubBusinessContactsClient(params BookingBusinessContact[] contacts) : IBookingBusinessContactsClient
    {
        public BookingBusinessContactSearch? LastSearch { get; private set; }

        public Task<IReadOnlyList<BookingBusinessContact>> GetContactsAsync(
            BookingBusinessContactSearch search,
            CancellationToken ct)
        {
            LastSearch = search;
            return Task.FromResult<IReadOnlyList<BookingBusinessContact>>(contacts
                .Where(contact => search.ContactTypes.Contains(contact.ContactType, StringComparer.OrdinalIgnoreCase))
                .ToArray());
        }
    }
}
