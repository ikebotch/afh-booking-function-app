using AFH.Notification.Application.Policies.Booking;
using AFH.Notification.Application.Services;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Booking.Tests;

public sealed class NotificationRecipientResolverTests
{
    [Fact]
    public async Task ResolveAsync_ClientAction_RoutesClientAdviserAndContactCentreRecipients()
    {
        var resolver = CreateResolver();

        var route = await resolver.ResolveAsync(
            CreateNotification(
                LifecycleActors.Client,
                [
                    ClientRecipient(),
                    AdviserRecipient(),
                    ContactCentreRecipient(),
                    InternalRecipient()
                ]),
            CancellationToken.None);

        Assert.Equal(
            [BookingNotificationRecipientTypes.Client, BookingNotificationRecipientTypes.Adviser, BookingNotificationRecipientTypes.ContactCentre],
            route.Recipients.Select(x => x.RecipientType).ToArray());
        Assert.True(route.CopyContactCentre);
    }

    [Fact]
    public async Task ResolveAsync_AdminAction_AllowsInternalRecipientsAndMarksContactCentreCopy()
    {
        var resolver = CreateResolver();

        var route = await resolver.ResolveAsync(
            CreateNotification(
                "Admin",
                [
                    ClientRecipient(),
                    AdviserRecipient(),
                    ContactCentreRecipient(),
                    InternalRecipient()
                ]),
            CancellationToken.None);

        Assert.Equal(
            [BookingNotificationRecipientTypes.Client, BookingNotificationRecipientTypes.Adviser, BookingNotificationRecipientTypes.ContactCentre, BookingNotificationRecipientTypes.Internal],
            route.Recipients.Select(x => x.RecipientType).ToArray());
        Assert.True(route.CopyContactCentre);
    }

    [Fact]
    public async Task ResolveAsync_InfersEmailSmsAndPushChannelsWithoutMakingNotificationEmailOnly()
    {
        var resolver = CreateResolver();

        var route = await resolver.ResolveAsync(
            CreateNotification(
                LifecycleActors.System,
                [
                    new NotificationRecipient(
                        BookingNotificationRecipientTypes.Client,
                        "Jane Client",
                        "jane@example.test",
                        "+447700900123",
                        "push-target-1")
                ]),
            CancellationToken.None);

        var recipient = Assert.Single(route.Recipients);
        Assert.Equal(
            [NotificationChannel.Email, NotificationChannel.Sms, NotificationChannel.Push],
            recipient.PreferredChannels);
    }

    [Fact]
    public async Task ResolveAsync_PreservesExplicitChannelsAndDeduplicatesRecipients()
    {
        var resolver = CreateResolver();
        var recipient = new NotificationRecipient(
            BookingNotificationRecipientTypes.Client,
            "Jane Client",
            "jane@example.test",
            "+447700900123",
            null,
            [NotificationChannel.Sms, NotificationChannel.Sms, NotificationChannel.Unknown]);

        var route = await resolver.ResolveAsync(
            CreateNotification(LifecycleActors.Client, [recipient, recipient]),
            CancellationToken.None);

        var routedRecipient = Assert.Single(route.Recipients);
        Assert.Equal([NotificationChannel.Sms], routedRecipient.PreferredChannels);
    }

    private static NotificationRequested CreateNotification(
        string actorType,
        IReadOnlyList<NotificationRecipient> recipients)
        => new NotificationRequested(
            BookingNotificationTypes.BookingConfirmed,
            "booking-1",
            new NotificationActor(actorType, "Booking", "actor-1", "Actor One", "actor@example.test"),
            recipients,
            new Dictionary<string, string>
            {
                ["eventId"] = "event-1"
            });

    private static NotificationRecipientResolver CreateResolver()
        => new([new BookingNotificationRoutingPolicy()]);

    private static NotificationRecipient ClientRecipient()
        => new(
            BookingNotificationRecipientTypes.Client,
            "Jane Client",
            "jane@example.test");

    private static NotificationRecipient AdviserRecipient()
        => new(
            BookingNotificationRecipientTypes.Adviser,
            "Alex Adviser",
            "alex@example.test");

    private static NotificationRecipient ContactCentreRecipient()
        => new(
            BookingNotificationRecipientTypes.ContactCentre,
            "Contact Centre",
            "contact-centre@example.test");

    private static NotificationRecipient InternalRecipient()
        => new(
            BookingNotificationRecipientTypes.Internal,
            "Internal Ops",
            "ops@example.test");
}
