using System.Security.Cryptography;
using System.Text;
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
        var deliveryAudit = new StubNotificationDeliveryAuditStore();
        var delivery = new StubNotificationDeliveryGateway(NotificationChannel.Email);
        var service = new NotificationService(
            audit,
            deliveryAudit,
            CreateRecipientResolver(),
            CreateTemplateRenderer(),
            [delivery],
            NullLogger<NotificationService>.Instance);

        await service.PublishAsync(
            new NotificationRequested(
                new NotificationType("Booking", "BookingConfirmed"),
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
                    ["manageBookingLinks"] = string.Empty,
                    ["viewBookingUrl"] = "https://client.example/bookings/booking-1?token=token",
                    ["cancelBookingUrl"] = "https://client.example/bookings/booking-1/cancel?token=token",
                    ["rescheduleBookingUrl"] = "https://client.example/bookings/booking-1/reschedule?token=token",
                    ["TemplateKey"] = "booking-confirmed",
                    ["TemplateVersion"] = "v1"
                }),
            CancellationToken.None);

        Assert.NotNull(audit.LastNotification);
        var request = Assert.Single(delivery.Requests);
        Assert.Equal(NotificationChannel.Email, request.Channel);
        Assert.Equal("jane@example.test", request.Recipient.Email);
        Assert.Equal("AFH Booking: Booking Confirmed", request.Subject);
        Assert.Contains("Your booking is now confirmed.", request.TextBody);
        Assert.NotNull(request.HtmlBody);
        Assert.Contains("<a href=\"https://client.example/bookings/booking-1?token=token\">View booking</a>", request.HtmlBody);
        Assert.Contains("<a href=\"https://client.example/bookings/booking-1/cancel?token=token\">Cancel booking</a>", request.HtmlBody);
        Assert.Contains("<a href=\"https://client.example/bookings/booking-1/reschedule?token=token\">Reschedule booking</a>", request.HtmlBody);
        Assert.Equal("BookingConfirmed", request.ProviderMetadata?["notificationType"]);
        Assert.Equal(LifecycleActors.Client, request.ProviderMetadata?["actorType"]);
        Assert.Equal("Booking", request.ProviderMetadata?["actorSourceApplication"]);
        var dispatch = Assert.Single(deliveryAudit.Records);
        Assert.Equal("Booking", dispatch.SourceReferenceType);
        Assert.Equal("booking-1", dispatch.SourceReferenceId);
        Assert.Equal("BookingConfirmed", dispatch.NotificationType);
        Assert.Equal("Email", dispatch.Channel);
        Assert.Equal("Composed", dispatch.ProviderName);
        Assert.Equal("provider-1", dispatch.ProviderMessageId);
        Assert.Equal("booking-confirmed", dispatch.TemplateKey);
        Assert.Equal("v1", dispatch.TemplateVersion);
        Assert.Null(dispatch.FailureDetails);
        Assert.Null(dispatch.MessageSubject);
        Assert.Null(dispatch.MessageBody);

        var messageLog = Assert.Single(deliveryAudit.Records.Select(x => x.MessageLog));
        Assert.NotNull(messageLog);
        Assert.Equal(dispatch.DispatchUid, messageLog!.NotificationDispatchId);
        Assert.Equal(request.Subject, messageLog.Subject);
        Assert.Equal(request.TextBody, messageLog.Body);
        Assert.Equal("booking-confirmed", messageLog.TemplateKey);
        Assert.Equal("v1", messageLog.TemplateVersion);
        Assert.Equal("text/html", messageLog.ContentType);
        Assert.Equal(ComputeSha256(request.TextBody), messageLog.BodyHash);
    }

    [Fact]
    public async Task PublishAsync_WithRecipientTypeKeysDifferingOnlyByCase_DoesNotThrow()
    {
        var deliveryAudit = new StubNotificationDeliveryAuditStore();
        var delivery = new StubNotificationDeliveryGateway(NotificationChannel.Email);
        var service = new NotificationService(
            new StubNotificationAuditStore(),
            deliveryAudit,
            CreateRecipientResolver(),
            CreateTemplateRenderer(),
            [delivery],
            NullLogger<NotificationService>.Instance);

        await service.PublishAsync(
            new NotificationRequested(
                new NotificationType("Booking", "BookingConfirmed"),
                "booking-recipient-type-case",
                new NotificationActor(LifecycleActors.Client, "Booking", "client-1", "Jane Client", "jane@example.test"),
                [
                    new NotificationRecipient(
                        BookingNotificationRecipientTypes.Client,
                        "Jane Client",
                        "jane@example.test",
                        PreferredChannels: [NotificationChannel.Email])
                ],
                new Dictionary<string, string>
                {
                    ["transactionRef"] = "TRX-CASE",
                    ["bookingId"] = "booking-recipient-type-case",
                    ["adviserName"] = "Alex Adviser",
                    ["meetingType"] = "Review",
                    ["when"] = "2026-03-26 12:00 (Europe/London) -> 2026-03-26 13:00 (Europe/London)",
                    ["whereLine"] = "Join link: https://meeting.example/join",
                    ["travelLine"] = "Travel: N/A (remote meeting)",
                    ["RecipientType"] = "Client",
                    ["recipientType"] = "Client",
                    ["TemplateKey"] = "booking-confirmed",
                    ["TemplateVersion"] = "v1"
                }),
            CancellationToken.None);

        Assert.Single(delivery.Requests);
        var dispatch = Assert.Single(deliveryAudit.Records);
        Assert.Equal(BookingNotificationRecipientTypes.Client, dispatch.RecipientType);
        var messageLog = Assert.Single(deliveryAudit.Records.Select(x => x.MessageLog));
        Assert.NotNull(messageLog);
        Assert.DoesNotContain("\"RecipientType\"", messageLog!.RenderDataJson);
        Assert.Contains("\"recipientType\"", messageLog.RenderDataJson);
    }

    [Fact]
    public async Task PublishAsync_BookingConfirmed_MixedRecipientsDoesNotSendClientTokenLinksToAdviser()
    {
        var deliveryAudit = new StubNotificationDeliveryAuditStore();
        var delivery = new StubNotificationDeliveryGateway(NotificationChannel.Email);
        var service = new NotificationService(
            new StubNotificationAuditStore(),
            deliveryAudit,
            CreateRecipientResolver(),
            CreateTemplateRenderer(),
            [delivery],
            NullLogger<NotificationService>.Instance);

        await service.PublishAsync(
            new NotificationRequested(
                new NotificationType("Booking", "BookingConfirmed"),
                "booking-1",
                new NotificationActor(LifecycleActors.Client, "Booking", "client-1", "Jane Client", "jane@example.test"),
                [
                    new NotificationRecipient(
                        BookingNotificationRecipientTypes.Client,
                        "Jane Client",
                        "jane@example.test",
                        PreferredChannels: [NotificationChannel.Email]),
                    new NotificationRecipient(
                        BookingNotificationRecipientTypes.Adviser,
                        "Alex Adviser",
                        "adviser@example.test",
                        PreferredChannels: [NotificationChannel.Email])
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
                    ["manageBookingLinks"] = "View: https://client.example/bookings/booking-1?token=secret",
                    ["viewBookingUrl"] = "https://client.example/bookings/booking-1?token=secret",
                    ["cancelBookingUrl"] = "https://client.example/bookings/booking-1/cancel?token=secret",
                    ["rescheduleBookingUrl"] = "https://client.example/bookings/booking-1/reschedule?token=secret",
                    ["bookingChangeToken"] = "secret",
                    ["TemplateKey"] = "booking-confirmed",
                    ["TemplateVersion"] = "v1"
                }),
            CancellationToken.None);

        Assert.Equal(2, delivery.Requests.Count);
        var clientRequest = delivery.Requests.Single(x => x.Recipient.RecipientType == BookingNotificationRecipientTypes.Client);
        var adviserRequest = delivery.Requests.Single(x => x.Recipient.RecipientType == BookingNotificationRecipientTypes.Adviser);

        Assert.Equal("AFH Booking: Booking Confirmed", clientRequest.Subject);
        Assert.Contains("token=secret", clientRequest.TextBody, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("AFH Booking: Booking Confirmed (Adviser)", adviserRequest.Subject);
        Assert.Contains("authenticated Control Centre booking record", adviserRequest.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token=secret", adviserRequest.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("client.example", adviserRequest.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.Null(adviserRequest.HtmlBody);

        var adviserLog = Assert.Single(deliveryAudit.Records
            .Select(x => x.MessageLog)
            .Where(x => x?.RecipientType == BookingNotificationRecipientTypes.Adviser));
        Assert.NotNull(adviserLog);
        Assert.Equal("booking-confirmed-adviser", adviserLog!.TemplateKey);
        Assert.DoesNotContain("token=secret", adviserLog.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("viewBookingUrl", adviserLog.RenderDataJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bookingChangeToken", adviserLog.RenderDataJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublishAsync_FiltersAttachmentsByRecipientAndChannel()
    {
        var emailDelivery = new StubNotificationDeliveryGateway(NotificationChannel.Email);
        var service = new NotificationService(
            new StubNotificationAuditStore(),
            new StubNotificationDeliveryAuditStore(),
            CreateRecipientResolver(),
            CreateTemplateRenderer(),
            [emailDelivery],
            NullLogger<NotificationService>.Instance);

        await service.PublishAsync(
            new NotificationRequested(
                new NotificationType("Booking", "BookingConfirmed"),
                "booking-1",
                new NotificationActor(LifecycleActors.Client, "Booking", "client-1", "Jane Client", "jane@example.test"),
                [
                    new NotificationRecipient(
                        BookingNotificationRecipientTypes.Client,
                        "Jane Client",
                        "jane@example.test",
                        PreferredChannels: [NotificationChannel.Email]),
                    new NotificationRecipient(
                        BookingNotificationRecipientTypes.Manager,
                        "Manager",
                        "manager@example.test",
                        PreferredChannels: [NotificationChannel.Email])
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
                    ["TemplateKey"] = "booking-confirmed",
                    ["TemplateVersion"] = "v1"
                },
                [
                    new NotificationAttachment(
                        "booking.ics",
                        "text/calendar; charset=utf-8; method=REQUEST",
                        Convert.ToBase64String(Encoding.UTF8.GetBytes("BEGIN:VCALENDAR")),
                        RecipientTypes: [BookingNotificationRecipientTypes.Client],
                        Channels: [NotificationChannel.Email]),
                    new NotificationAttachment(
                        "manager-sms-only.ics",
                        "text/calendar; charset=utf-8; method=REQUEST",
                        Convert.ToBase64String(Encoding.UTF8.GetBytes("BEGIN:VCALENDAR")),
                        RecipientTypes: [BookingNotificationRecipientTypes.Manager],
                        Channels: [NotificationChannel.Sms]),
                    new NotificationAttachment(
                        "adviser-only.ics",
                        "text/calendar; charset=utf-8; method=REQUEST",
                        Convert.ToBase64String(Encoding.UTF8.GetBytes("BEGIN:VCALENDAR")),
                        RecipientTypes: [BookingNotificationRecipientTypes.Adviser],
                        Channels: [NotificationChannel.Email])
                ]),
            CancellationToken.None);

        var clientEmail = Assert.Single(emailDelivery.Requests, x => x.Recipient.RecipientType == BookingNotificationRecipientTypes.Client);
        var managerEmail = Assert.Single(emailDelivery.Requests, x => x.Recipient.RecipientType == BookingNotificationRecipientTypes.Manager);

        var attachment = Assert.Single(clientEmail.Attachments!);
        Assert.Equal("booking.ics", attachment.FileName);
        Assert.Null(managerEmail.Attachments);
    }

    [Fact]
    public async Task PublishAsync_BookingRescheduled_RendersTemplateAndSendsDeliveryRequest()
    {
        var audit = new StubNotificationAuditStore();
        var deliveryAudit = new StubNotificationDeliveryAuditStore();
        var delivery = new StubNotificationDeliveryGateway(NotificationChannel.Email);
        var service = new NotificationService(
            audit,
            deliveryAudit,
            CreateRecipientResolver(),
            CreateTemplateRenderer(),
            [delivery],
            NullLogger<NotificationService>.Instance);

        await service.PublishAsync(
            new NotificationRequested(
                new NotificationType("Booking", "BookingRescheduled"),
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
        var deliveryAudit = new StubNotificationDeliveryAuditStore();
        var delivery = new StubNotificationDeliveryGateway(NotificationChannel.Email);
        var service = new NotificationService(
            audit,
            deliveryAudit,
            CreateRecipientResolver(),
            CreateTemplateRenderer(),
            [delivery],
            NullLogger<NotificationService>.Instance);

        await service.PublishAsync(
            new NotificationRequested(
                new NotificationType("Booking", "BookingCancelled"),
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
        var deliveryAudit = new StubNotificationDeliveryAuditStore();
        var delivery = new StubNotificationDeliveryGateway(NotificationChannel.Email);
        var service = new NotificationService(
            audit,
            deliveryAudit,
            CreateRecipientResolver(),
            CreateTemplateRenderer(),
            [delivery],
            NullLogger<NotificationService>.Instance);

        await service.PublishAsync(
            new NotificationRequested(
                new NotificationType("Booking", "BookingHoldCreated"),
                "hold-1",
                new NotificationActor(LifecycleActors.System, "Booking", null, null, null),
                [
                    new NotificationRecipient(
                        BookingNotificationRecipientTypes.Client,
                        "Jane Client",
                        "jane@example.test"),
                    new NotificationRecipient(
                        BookingNotificationRecipientTypes.ContactCentre,
                        "Contact Centre",
                        "contact@centre.test")
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
        Assert.Equal(2, deliveryAudit.Records.Count);
        Assert.Contains(deliveryAudit.Records, record => record.RecipientEmail == "jane@example.test");
        Assert.Contains(deliveryAudit.Records, record => record.RecipientEmail == "contact@centre.test");
    }

    [Fact]
    public async Task PublishAsync_FailedDeliveryRecordsFailedAuditAndRethrows()
    {
        var audit = new StubNotificationAuditStore();
        var deliveryAudit = new StubNotificationDeliveryAuditStore();
        var delivery = new GraphFailingNotificationDeliveryGateway(NotificationChannel.Email);
        var service = new NotificationService(
            audit,
            deliveryAudit,
            CreateRecipientResolver(),
            CreateTemplateRenderer(),
            [delivery],
            NullLogger<NotificationService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PublishAsync(
            new NotificationRequested(
                new NotificationType("Booking", "BookingConfirmed"),
                "booking-1",
                new NotificationActor(LifecycleActors.Client, "Booking", "client-1", "Jane Client", "jane@example.test"),
                [new NotificationRecipient(BookingNotificationRecipientTypes.Client, "Jane Client", "jane@example.test")],
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
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            CancellationToken.None));

        var dispatch = Assert.Single(deliveryAudit.Records);
        Assert.Equal(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), dispatch.NotificationOutboxId);
        Assert.Equal("Failed", dispatch.Status);
        Assert.Equal("Graph", dispatch.ProviderName);
        Assert.Contains("Graph failed", dispatch.FailureDetails);
        Assert.DoesNotContain("Your booking is now confirmed", dispatch.FailureDetails);

        var messageLog = Assert.Single(deliveryAudit.Records.Select(x => x.MessageLog));
        Assert.NotNull(messageLog);
        Assert.Equal(dispatch.DispatchUid, messageLog!.NotificationDispatchId);
        Assert.Equal("AFH Booking: Booking Confirmed", messageLog.Subject);
        Assert.Contains("Your booking is now confirmed", messageLog.Body);
        Assert.Equal(ComputeSha256(messageLog.Body), messageLog.BodyHash);
        Assert.Null(dispatch.MessageBody);
    }

    [Fact]
    public async Task PublishAsync_EmailAndSmsEnabled_CreateSeparateDeliveryAttempts()
    {
        var deliveryAudit = new StubNotificationDeliveryAuditStore();
        var emailDelivery = new StubNotificationDeliveryGateway(NotificationChannel.Email, "Graph", "email-provider-1");
        var smsDelivery = new StubNotificationDeliveryGateway(NotificationChannel.Sms, "Twilio", "sms-provider-1");
        var service = new NotificationService(
            new StubNotificationAuditStore(),
            deliveryAudit,
            CreateRecipientResolver(),
            CreateTemplateRendererWithDbTemplates(),
            [emailDelivery, smsDelivery],
            NullLogger<NotificationService>.Instance);

        await service.PublishAsync(CreateEmailAndSmsRequest(), CancellationToken.None);

        Assert.Single(emailDelivery.Requests);
        Assert.Single(smsDelivery.Requests);
        Assert.Equal("jane@example.test", emailDelivery.Requests[0].Recipient.Email);
        Assert.Equal("+447700900000", smsDelivery.Requests[0].Recipient.MobileNumber);
        Assert.Equal("SMS body booking-1", smsDelivery.Requests[0].TextBody);

        var emailDispatch = Assert.Single(deliveryAudit.Records, x => x.Channel == "Email");
        var smsDispatch = Assert.Single(deliveryAudit.Records, x => x.Channel == "Sms");
        Assert.Equal("Graph", emailDispatch.ProviderName);
        Assert.Equal("Twilio", smsDispatch.ProviderName);
        Assert.Equal("sms-provider-1", smsDispatch.ProviderMessageId);
        Assert.Equal("+447700900000", smsDispatch.RecipientMobile);
        Assert.Null(smsDispatch.RecipientEmail);
        Assert.Equal("SMS body booking-1", smsDispatch.MessageLog?.Body);
        Assert.Null(smsDispatch.MessageLog?.Subject);
    }

    [Fact]
    public async Task PublishAsync_DuplicateMobileChannel_SendsSmsOnce()
    {
        var smsDelivery = new StubNotificationDeliveryGateway(NotificationChannel.Sms, "Twilio", "sms-provider-1");
        var service = new NotificationService(
            new StubNotificationAuditStore(),
            new StubNotificationDeliveryAuditStore(),
            CreateRecipientResolver(),
            CreateTemplateRendererWithDbTemplates(),
            [smsDelivery],
            NullLogger<NotificationService>.Instance);

        var request = CreateEmailAndSmsRequest() with
        {
            Recipients =
            [
                new NotificationRecipient("Client", "Jane Client", null, "+447700900000", null, [NotificationChannel.Sms]),
                new NotificationRecipient("Client", "Jane Client duplicate", null, "+447700900000", null, [NotificationChannel.Sms])
            ]
        };

        await service.PublishAsync(request, CancellationToken.None);

        Assert.Single(smsDelivery.Requests);
    }

    [Fact]
    public async Task PublishAsync_MissingMobile_SkipsSmsRecipientChannelOnly()
    {
        var deliveryAudit = new StubNotificationDeliveryAuditStore();
        var emailDelivery = new StubNotificationDeliveryGateway(NotificationChannel.Email, "Graph", "email-provider-1");
        var smsDelivery = new StubNotificationDeliveryGateway(NotificationChannel.Sms, "Twilio", "sms-provider-1");
        var service = new NotificationService(
            new StubNotificationAuditStore(),
            deliveryAudit,
            CreateRecipientResolver(),
            CreateTemplateRendererWithDbTemplates(),
            [emailDelivery, smsDelivery],
            NullLogger<NotificationService>.Instance);

        var request = CreateEmailAndSmsRequest() with
        {
            Recipients =
            [
                new NotificationRecipient("Client", "Jane Client", "jane@example.test", null, null, [NotificationChannel.Email, NotificationChannel.Sms])
            ]
        };

        await service.PublishAsync(request, CancellationToken.None);

        Assert.Single(emailDelivery.Requests);
        Assert.Single(smsDelivery.Requests);
        Assert.Equal("Skipped", Assert.Single(deliveryAudit.Records, x => x.Channel == "Sms").Status);
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
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

    private sealed class StubNotificationDeliveryAuditStore : INotificationDeliveryAuditStore
    {
        public List<NotificationDeliveryAuditRecord> Records { get; } = [];

        public Task RecordAttemptAsync(NotificationDeliveryAuditRecord record, CancellationToken ct)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }
    }

    private sealed class StubNotificationDeliveryGateway(
        NotificationChannel channel,
        string providerName = "Composed",
        string providerMessageId = "provider-1") : INotificationDeliveryGateway
    {
        public List<NotificationDeliveryRequest> Requests { get; } = [];

        public bool CanSend(NotificationChannel candidate)
            => candidate == channel;

        public Task<NotificationDeliveryResult> SendAsync(NotificationDeliveryRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            if (channel == NotificationChannel.Sms && string.IsNullOrWhiteSpace(request.Recipient.MobileNumber))
                return Task.FromResult(new NotificationDeliveryResult("Skipped", null, providerName));

            return Task.FromResult(new NotificationDeliveryResult("Sent", providerMessageId, providerName));
        }
    }

    private sealed class GraphFailingNotificationDeliveryGateway(NotificationChannel channel) : INotificationDeliveryGateway
    {
        public bool CanSend(NotificationChannel candidate)
            => candidate == channel;

        public Task<NotificationDeliveryResult> SendAsync(NotificationDeliveryRequest request, CancellationToken ct)
            => throw new InvalidOperationException("Graph failed.");
    }

    private static NotificationRecipientResolver CreateRecipientResolver()
        => new([new BookingNotificationRoutingPolicy()]);

    private static NotificationTemplateRenderer CreateTemplateRenderer()
        => new([new BookingNotificationTemplatePolicy()]);

    private static NotificationTemplateRenderer CreateTemplateRendererWithDbTemplates()
        => new(
            [new BookingNotificationTemplatePolicy()],
            new StubTemplateStore(
            [
                new NotificationTemplateDefinition(
                    "booking-confirmed",
                    "v1",
                    NotificationChannel.Email,
                    "Email confirmed",
                    null,
                    "Email subject {{bookingId}}",
                    "Email body {{bookingId}}",
                    "text/plain",
                    true),
                new NotificationTemplateDefinition(
                    "booking-confirmed",
                    "v1",
                    NotificationChannel.Sms,
                    "SMS confirmed",
                    null,
                    null,
                    "SMS body {{bookingId}}",
                    "text/plain",
                    true)
            ]));

    private static NotificationRequested CreateEmailAndSmsRequest()
        => new(
            new NotificationType("Booking", "BookingConfirmed"),
            "booking-1",
            new NotificationActor(LifecycleActors.System, "Booking", null, null, null),
            [
                new NotificationRecipient("Client", "Jane Client", "jane@example.test", null, null, [NotificationChannel.Email]),
                new NotificationRecipient("Client", "Jane Client", null, "+447700900000", null, [NotificationChannel.Sms])
            ],
            new Dictionary<string, string>
            {
                ["TemplateKey"] = "booking-confirmed",
                ["TemplateVersion"] = "v1",
                ["bookingId"] = "booking-1"
            });

    private sealed class StubTemplateStore(IReadOnlyCollection<NotificationTemplateDefinition> templates) : INotificationTemplateStore
    {
        public Task<NotificationTemplateDefinition?> GetAsync(
            string templateKey,
            string templateVersion,
            NotificationChannel channel,
            CancellationToken ct)
            => Task.FromResult(templates.SingleOrDefault(template =>
                template.TemplateKey == templateKey &&
                template.TemplateVersion == templateVersion &&
                template.Channel == channel));
    }
}
