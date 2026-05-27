using AFH.Notification.Application.Services;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Booking.Tests;

public sealed class NotificationRecipientResolverTests
{
    [Fact]
    public async Task ResolveAsync_ClientAction_RoutesClientAdviserAndContactCentreRecipients()
    {
        var resolver = new NotificationRecipientResolver();

        var route = await resolver.ResolveAsync(
            CreateNotification(
                BookingNotificationActorTypes.Client,
                [
                    ClientRecipient(),
                    AdviserRecipient(),
                    ContactCentreRecipient(),
                    InternalRecipient()
                ]),
            CancellationToken.None);

        Assert.Equal(
            [NotificationRecipientType.Client, NotificationRecipientType.Adviser, NotificationRecipientType.ContactCentre],
            route.Recipients.Select(x => x.Type).ToArray());
        Assert.True(route.CopyContactCentre);
    }

    [Fact]
    public async Task ResolveAsync_AdminAction_AllowsInternalRecipientsAndMarksContactCentreCopy()
    {
        var resolver = new NotificationRecipientResolver();

        var route = await resolver.ResolveAsync(
            CreateNotification(
                BookingNotificationActorTypes.Admin,
                [
                    ClientRecipient(),
                    AdviserRecipient(),
                    ContactCentreRecipient(),
                    InternalRecipient()
                ]),
            CancellationToken.None);

        Assert.Equal(
            [NotificationRecipientType.Client, NotificationRecipientType.Adviser, NotificationRecipientType.ContactCentre, NotificationRecipientType.Internal],
            route.Recipients.Select(x => x.Type).ToArray());
        Assert.True(route.CopyContactCentre);
    }

    [Fact]
    public async Task ResolveAsync_InfersEmailSmsAndPushChannelsWithoutMakingNotificationEmailOnly()
    {
        var resolver = new NotificationRecipientResolver();

        var route = await resolver.ResolveAsync(
            CreateNotification(
                BookingNotificationActorTypes.System,
                [
                    new NotificationRecipient(
                        NotificationRecipientType.Client,
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
        var resolver = new NotificationRecipientResolver();
        var recipient = new NotificationRecipient(
            NotificationRecipientType.Client,
            "Jane Client",
            "jane@example.test",
            "+447700900123",
            null,
            [NotificationChannel.Sms, NotificationChannel.Sms, NotificationChannel.Unknown]);

        var route = await resolver.ResolveAsync(
            CreateNotification(BookingNotificationActorTypes.Client, [recipient, recipient]),
            CancellationToken.None);

        var routedRecipient = Assert.Single(route.Recipients);
        Assert.Equal([NotificationChannel.Sms], routedRecipient.PreferredChannels);
    }

    private static NotificationRequested CreateNotification(
        string actorType,
        IReadOnlyList<NotificationRecipient> recipients)
        => NotificationRequested.BookingConfirmed(
            "booking-1",
            new NotificationActor(actorType, "Booking", "actor-1", "Actor One", "actor@example.test"),
            recipients,
            new Dictionary<string, string>
            {
                ["eventId"] = "event-1"
            });

    private static NotificationRecipient ClientRecipient()
        => new(
            NotificationRecipientType.Client,
            "Jane Client",
            "jane@example.test");

    private static NotificationRecipient AdviserRecipient()
        => new(
            NotificationRecipientType.Adviser,
            "Alex Adviser",
            "alex@example.test");

    private static NotificationRecipient ContactCentreRecipient()
        => new(
            NotificationRecipientType.ContactCentre,
            "Contact Centre",
            "contact-centre@example.test");

    private static NotificationRecipient InternalRecipient()
        => new(
            NotificationRecipientType.Internal,
            "Internal Ops",
            "ops@example.test");
}
