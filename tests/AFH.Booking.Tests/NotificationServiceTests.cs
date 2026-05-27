using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
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
            new NotificationRecipientResolver(),
            new NotificationTemplateRenderer(),
            [delivery],
            NullLogger<NotificationService>.Instance);

        await service.PublishAsync(
            new NotificationRequested(
                BookingNotificationTypes.BookingConfirmed,
                "booking-1",
                new NotificationActor(BookingNotificationActorTypes.Client, "Booking", "client-1", "Jane Client", "jane@example.test"),
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
        Assert.Equal(BookingNotificationActorTypes.Client, request.ProviderMetadata?["actorType"]);
        Assert.Equal("Booking", request.ProviderMetadata?["actorSourceApplication"]);
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
}
