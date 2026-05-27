using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Application.Policies.Booking;
using AFH.Notification.Application.Services;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFH.Booking.Tests;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task PublishAsync_BookingConfirmed_RendersTemplateAndSendsDeliveryRequest()
    {
        var audit = new StubNotificationAuditStore();
        var delivery = new StubNotificationDeliveryGateway(NotificationChannel.Email);
        var service = new NotificationService(
            audit,
            CreateRecipientResolver(),
            CreateTemplateRenderer(),
            new StubContactCentreRoutingResolver(),
            [delivery],
            NullLogger<NotificationService>.Instance);

        await service.PublishAsync(
            new NotificationRequested(
                BookingNotificationTypes.BookingConfirmed,
                "booking-1",
                new NotificationActor(LifecycleActors.Client, "Booking", "client-1", "Jane Client", "jane@example.test"),
                [
                    new NotificationRecipient(
                        BookingNotificationRecipientTypes.Client,
                        "Jane Client",
                        "jane@example.test")
                ],
                new Dictionary<string, string>
                {
                    ["transactionRef"] = "TRX-1",
                    ["bookingId"] = "booking-1",
                    ["adviserName"] = "Alex Adviser",
                    ["meetingType"] = "Review",
                    ["when"] = "2026-03-26 12:00 (Europe/London) -> 2026-03-26 13:00 (Europe/London)",
                    ["whereLine"] = "Join link: https://meeting.example/join",
                    ["travelLine"] = "Travel: N/A (remote meeting)",
                    ["manageBookingLinks"] = string.Empty
                }),
            CancellationToken.None);

        Assert.NotNull(audit.LastNotification);
        var request = Assert.Single(delivery.Requests);
        Assert.Equal(NotificationChannel.Email, request.Channel);
        Assert.Equal("jane@example.test", request.Recipient.Email);
        Assert.Equal("AFH Booking: Booking Confirmed", request.Subject);
        Assert.Contains("Your booking is now confirmed.", request.TextBody);
        Assert.Equal("BookingConfirmed", request.ProviderMetadata?["notificationType"]);
        Assert.Equal(LifecycleActors.Client, request.ProviderMetadata?["actorType"]);
        Assert.Equal("Booking", request.ProviderMetadata?["actorSourceApplication"]);
    }

    [Fact]
    public async Task PublishAsync_BookingRescheduled_RendersTemplateAndSendsDeliveryRequest()
    {
        var audit = new StubNotificationAuditStore();
        var delivery = new StubNotificationDeliveryGateway(NotificationChannel.Email);
        var service = new NotificationService(
            audit,
            CreateRecipientResolver(),
            CreateTemplateRenderer(),
            new StubContactCentreRoutingResolver(),
            [delivery],
            NullLogger<NotificationService>.Instance);

        await service.PublishAsync(
            new NotificationRequested(
                BookingNotificationTypes.BookingRescheduled,
                "booking-2",
                new NotificationActor(LifecycleActors.Client, "Booking", "client-1", "Jane Client", "jane@example.test"),
                [
                    new NotificationRecipient(
                        BookingNotificationRecipientTypes.Client,
                        "Jane Client",
                        "jane@example.test")
                ],
                new Dictionary<string, string>
                {
                    ["greetingName"] = "there",
                    ["whenLine"] = "Thu 26 Mar 2026 09:00 (Europe/London) to Thu 26 Mar 2026 10:00 (Europe/London)",
                    ["adviserName"] = "Alex Adviser",
                    ["locationLine"] = "Remote meeting",
                    ["note"] = "Your meeting time has changed.",
                    ["manageBookingLinks"] = string.Empty
                }),
            CancellationToken.None);

        Assert.NotNull(audit.LastNotification);
        var request = Assert.Single(delivery.Requests);
        Assert.Equal(NotificationChannel.Email, request.Channel);
        Assert.Equal("jane@example.test", request.Recipient.Email);
        Assert.Equal("AFH Booking: Appointment Rescheduled", request.Subject);
        Assert.Contains("Appointment Rescheduled", request.TextBody);
        Assert.Contains("Your meeting time has changed.", request.TextBody);
        Assert.Equal("BookingRescheduled", request.ProviderMetadata?["notificationType"]);
    }

    [Fact]
    public async Task PublishAsync_BookingCancelled_RendersTemplateAndSendsDeliveryRequest()
    {
        var audit = new StubNotificationAuditStore();
        var delivery = new StubNotificationDeliveryGateway(NotificationChannel.Email);
        var service = new NotificationService(
            audit,
            CreateRecipientResolver(),
            CreateTemplateRenderer(),
            new StubContactCentreRoutingResolver(),
            [delivery],
            NullLogger<NotificationService>.Instance);

        await service.PublishAsync(
            new NotificationRequested(
                BookingNotificationTypes.BookingCancelled,
                "booking-3",
                new NotificationActor(LifecycleActors.Client, "Booking", "client-1", "Jane Client", "jane@example.test"),
                [
                    new NotificationRecipient(
                        BookingNotificationRecipientTypes.Client,
                        "Jane Client",
                        "jane@example.test")
                ],
                new Dictionary<string, string>
                {
                    ["greetingName"] = "there",
                    ["whenLine"] = "Thu 26 Mar 2026 09:00 (Europe/London) to Thu 26 Mar 2026 10:00 (Europe/London)",
                    ["adviserName"] = "Alex Adviser",
                    ["locationLine"] = "Remote meeting",
                    ["note"] = "Your meeting with Alex Adviser on 2026-03-26 09:00 has been cancelled.",
                    ["manageBookingLinks"] = string.Empty
                }),
            CancellationToken.None);

        Assert.NotNull(audit.LastNotification);
        var request = Assert.Single(delivery.Requests);
        Assert.Equal(NotificationChannel.Email, request.Channel);
        Assert.Equal("jane@example.test", request.Recipient.Email);
        Assert.Equal("AFH Booking: Appointment Cancelled", request.Subject);
        Assert.Contains("Appointment Cancelled", request.TextBody);
        Assert.Contains("Your meeting with Alex Adviser on 2026-03-26 09:00 has been cancelled.", request.TextBody);
        Assert.Equal("BookingCancelled", request.ProviderMetadata?["notificationType"]);
    }

    [Fact]
    public async Task PublishAsync_BookingHoldCreated_RendersTemplateAndSendsDeliveryRequest()
    {
        var audit = new StubNotificationAuditStore();
        var delivery = new StubNotificationDeliveryGateway(NotificationChannel.Email);
        var service = new NotificationService(
            audit,
            CreateRecipientResolver(),
            CreateTemplateRenderer(),
            new StubContactCentreRoutingResolver(),
            [delivery],
            NullLogger<NotificationService>.Instance);

        await service.PublishAsync(
            new NotificationRequested(
                BookingNotificationTypes.BookingHoldCreated,
                "hold-1",
                new NotificationActor(LifecycleActors.System, "Booking", null, null, null),
                [
                    new NotificationRecipient(
                        BookingNotificationRecipientTypes.Client,
                        "Jane Client",
                        "jane@example.test")
                ],
                new Dictionary<string, string>
                {
                    ["transactionRef"] = "TRX-1",
                    ["holdId"] = "hold-1",
                    ["adviserName"] = "Alex Adviser",
                    ["meetingType"] = "Remote meeting",
                    ["when"] = "Thu 26 Mar 2026 09:00 (Europe/London) to Thu 26 Mar 2026 10:00 (Europe/London)",
                    ["holdExpires"] = "Thu 26 Mar 2026 09:03 (Europe/London)",
                    ["travelLine"] = "Travel: N/A (remote meeting)",
                    ["companyLine"] = string.Empty,
                    ["manageBookingLinks"] = string.Empty
                }),
            CancellationToken.None);

        Assert.NotNull(audit.LastNotification);
        Assert.Equal(2, delivery.Requests.Count);
        var request = delivery.Requests.Single(r => r.Recipient.RecipientType == BookingNotificationRecipientTypes.Client);
        var ccRequest = delivery.Requests.Single(r => r.Recipient.RecipientType == "ContactCentre");
        
        Assert.Equal(NotificationChannel.Email, request.Channel);
        Assert.Equal("jane@example.test", request.Recipient.Email);
        Assert.Equal("contact@centre.test", ccRequest.Recipient.Email);
        Assert.Equal("AFH Booking: Hold Created", request.Subject);
        Assert.Contains("temporary hold", request.TextBody);
        Assert.Contains("Alex Adviser", request.TextBody);
        Assert.Contains("TRX-1", request.TextBody);
        Assert.Equal("BookingHoldCreated", request.ProviderMetadata?["notificationType"]);
    }

    private sealed class StubNotificationAuditStore : INotificationAuditStore
    {
        public NotificationRequested? LastNotification { get; private set; }

        public Task RecordRequestedAsync(NotificationRequested notification, CancellationToken ct)
        {
            LastNotification = notification;
            return Task.CompletedTask;
        }
    }

    private sealed class StubNotificationDeliveryGateway(NotificationChannel channel) : INotificationDeliveryGateway
    {
        public List<NotificationDeliveryRequest> Requests { get; } = [];

        public bool CanSend(NotificationChannel candidate)
            => candidate == channel;

        public Task<NotificationDeliveryResult> SendAsync(NotificationDeliveryRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new NotificationDeliveryResult("Composed", "provider-1"));
        }
    }

    private sealed class StubContactCentreRoutingResolver : IContactCentreRoutingResolver
    {
        public string? GetContactCentreEmailAddress() => "contact@centre.test";
    }

    private static NotificationRecipientResolver CreateRecipientResolver()
        => new([new BookingNotificationRoutingPolicy()]);

    private static NotificationTemplateRenderer CreateTemplateRenderer()
        => new([new BookingNotificationTemplatePolicy()]);
}
