using AFH.Booking.Application.EmailTemplates;
using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Calendar;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Application.Policies.Booking;
using AFH.Notification.Application.Services;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Booking.Tests;

public sealed class BookingNotificationTemplateTests
{
    [Fact]
    public void Build_GenericEmailTemplate_SeparatesHtmlTextAndCalendarContent()
    {
        var template = BookingNotificationEmailTemplate.Build(
            eventType: "Cancelled",
            clientDisplayName: "Jane Client",
            adviserName: "Alex Adviser",
            startUtc: new DateTime(2026, 03, 26, 9, 0, 0, DateTimeKind.Utc),
            endUtc: new DateTime(2026, 03, 26, 10, 0, 0, DateTimeKind.Utc),
            timezoneId: "Europe/London",
            isRemote: true,
            customMessage: "Please contact us if you need help rearranging.",
            viewUrl: "https://client.example/bookings/booking-1?token=token",
            cancelUrl: "https://client.example/bookings/booking-1/cancel?token=token",
            rescheduleUrl: "https://client.example/bookings/booking-1/reschedule?token=token");

        Assert.Contains("<html", template.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<html", template.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<table", template.CalendarDescription, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<body", template.CalendarDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Appointment Cancelled", template.Subject);
        Assert.Contains("Please contact us if you need help rearranging.", template.TextBody);
        Assert.Contains("Meeting type: Remote meeting", template.CalendarDescription);
        Assert.Contains("View: https://client.example/bookings/booking-1?token=token", template.TextBody);
        Assert.Contains("Cancel: https://client.example/bookings/booking-1/cancel?token=token", template.TextBody);
        Assert.Contains("Reschedule: https://client.example/bookings/booking-1/reschedule?token=token", template.TextBody);
        Assert.Contains("<a href=\"https://client.example/bookings/booking-1?token=token\"", template.HtmlBody);
        Assert.Contains("<a href=\"https://client.example/bookings/booking-1/reschedule?token=token\"", template.HtmlBody);
        Assert.Contains("<a href=\"https://client.example/bookings/booking-1/cancel?token=token\"", template.HtmlBody);
        Assert.DoesNotContain("<a href=", template.TextBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildHoldTemplate_UsesPlainCalendarDescription_NotHtmlMarkup()
    {
        var now = new DateTime(2026, 03, 26, 10, 0, 0, DateTimeKind.Utc);
        var transaction = CreateTransaction(now, isRemote: false);
        var slot = CreateSlot("slot-1", transaction.Id, now.AddHours(2), now.AddHours(3));
        var hold = BookingHold.Create(slot.Id, slot.AdviserId, TimeSpan.FromMinutes(10), now);
        var windows = new HoldWindows(slot.StartUtc.AddMinutes(-30), slot.EndUtc.AddMinutes(15), 15, 15, true);

        var template = HoldBookingTemplate.BuildHoldTemplate(slot, transaction, hold, windows, CreateLinks());

        Assert.Contains("<html", template.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<html", template.CalendarDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AFH Booking Hold", template.CalendarDescription);
        Assert.Contains("Hold expires", template.CalendarDescription);
        Assert.Contains("View booking: https://client.example/bookings/booking-1?token=token", template.TextBody);
        Assert.Contains("Cancel booking: https://client.example/bookings/booking-1/cancel?token=token", template.TextBody);
        Assert.Contains("Reschedule booking: https://client.example/bookings/booking-1/reschedule?token=token", template.TextBody);
        Assert.Contains("<a href=\"https://client.example/bookings/booking-1?token=token\"", template.HtmlBody);
        Assert.Contains("<a href=\"https://client.example/bookings/booking-1/cancel?token=token\"", template.HtmlBody);
        Assert.Contains("<a href=\"https://client.example/bookings/booking-1/reschedule?token=token\"", template.HtmlBody);
        Assert.DoesNotContain("<a href=", template.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("View booking: https://client.example/bookings/booking-1?token=token", template.CalendarDescription);
    }

    [Fact]
    public void BuildConfirmedTemplate_IncludesJoinLink_InTextAndCalendarDescription()
    {
        var now = new DateTime(2026, 03, 26, 10, 0, 0, DateTimeKind.Utc);
        var transaction = CreateTransaction(now, isRemote: true);
        var slot = CreateSlot("slot-1", transaction.Id, now.AddHours(2), now.AddHours(3));
        var hold = BookingHold.Create(slot.Id, slot.AdviserId, TimeSpan.FromMinutes(10), now);
        var windows = new HoldWindows(slot.StartUtc, slot.EndUtc, 0, 0, false);

        var template = ConfirmedBookingTemplate.BuildConfirmedTemplate(
            slot,
            transaction,
            hold,
            windows,
            joinUrl: "https://meeting.example/join",
            location: null,
            selfServiceLinks: CreateLinks());

        Assert.Contains("https://meeting.example/join", template.TextBody);
        Assert.Contains("https://meeting.example/join", template.CalendarDescription);
        Assert.Contains("View booking: https://client.example/bookings/booking-1?token=token", template.TextBody);
        Assert.Contains("Cancel booking: https://client.example/bookings/booking-1/cancel?token=token", template.TextBody);
        Assert.Contains("Reschedule booking: https://client.example/bookings/booking-1/reschedule?token=token", template.TextBody);
        Assert.Contains("<a href=\"https://client.example/bookings/booking-1?token=token\"", template.HtmlBody);
        Assert.Contains("<a href=\"https://client.example/bookings/booking-1/cancel?token=token\"", template.HtmlBody);
        Assert.Contains("<a href=\"https://client.example/bookings/booking-1/reschedule?token=token\"", template.HtmlBody);
        Assert.DoesNotContain("<a href=", template.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("View booking: https://client.example/bookings/booking-1?token=token", template.CalendarDescription);
        Assert.DoesNotContain("<html", template.CalendarDescription, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenderAsync_BookingConfirmedVersionedTemplate_RendersEmailLinksAsHtml()
    {
        var now = new DateTime(2026, 03, 26, 10, 0, 0, DateTimeKind.Utc);
        var transaction = CreateTransaction(now, isRemote: true);
        var slot = CreateSlot("slot-1", transaction.Id, now.AddHours(2), now.AddHours(3));
        var hold = BookingHold.Create(slot.Id, slot.AdviserId, TimeSpan.FromMinutes(10), now);
        var links = CreateLinks();

        var renderer = CreateRenderer();
        var rendered = await renderer.RenderAsync(
            new NotificationRequested(
                BookingNotificationTypes.BookingConfirmed,
                hold.Id,
                new NotificationActor(LifecycleActors.Client, "Booking", null, null, null),
                [],
                new Dictionary<string, string>
                {
                    ["transactionRef"] = transaction.TransactionRef,
                    ["bookingId"] = hold.Id,
                    ["adviserName"] = slot.AdviserName,
                    ["meetingType"] = transaction.MeetingType ?? "N/A",
                    ["when"] = "2026-03-26 12:00 (Europe/London) \u2192 2026-03-26 13:00 (Europe/London)",
                    ["whereLine"] = "Join link: https://meeting.example/join",
                    ["travelLine"] = "Travel: N/A (remote meeting)",
                    ["manageBookingLinks"] = BuildHoldManageLinks(links),
                    ["viewBookingUrl"] = links.ViewBookingUrl,
                    ["cancelBookingUrl"] = links.CancelBookingUrl,
                    ["rescheduleBookingUrl"] = links.RescheduleBookingUrl
                }),
            CancellationToken.None);

        var content = Assert.Single(rendered.ChannelContent);
        Assert.Equal(NotificationChannel.Email, content.Channel);
        Assert.Equal("AFH Booking: Booking Confirmed", content.Subject);
        Assert.Equal("text/html", content.ContentType);
        Assert.NotNull(content.HtmlBody);
        Assert.Contains("<a href=\"https://client.example/bookings/booking-1?token=token\">View booking</a>", content.HtmlBody);
        Assert.Contains("<a href=\"https://client.example/bookings/booking-1/cancel?token=token\">Cancel booking</a>", content.HtmlBody);
        Assert.Contains("<a href=\"https://client.example/bookings/booking-1/reschedule?token=token\">Reschedule booking</a>", content.HtmlBody);
        Assert.Contains("token=token", content.HtmlBody);
    }

    [Fact]
    public async Task RenderAsync_BookingRescheduledVersionedTemplate_MatchesExistingGenericTextOutput()
    {
        var links = CreateLinks();
        var existing = BookingNotificationEmailTemplate.Build(
            eventType: "Rescheduled",
            clientDisplayName: "Jane Client",
            adviserName: "Alex Adviser",
            startUtc: new DateTime(2026, 03, 26, 9, 0, 0, DateTimeKind.Utc),
            endUtc: new DateTime(2026, 03, 26, 10, 0, 0, DateTimeKind.Utc),
            timezoneId: "Europe/London",
            isRemote: true,
            customMessage: "Your booking has been moved to the selected time.",
            viewUrl: links.ViewBookingUrl,
            cancelUrl: links.CancelBookingUrl,
            rescheduleUrl: links.RescheduleBookingUrl);

        var renderer = CreateRenderer();
        var rendered = await renderer.RenderAsync(
            new NotificationRequested(
                BookingNotificationTypes.BookingRescheduled,
                "booking-1",
                new NotificationActor(LifecycleActors.Client, "Booking", null, null, null),
                [],
                new Dictionary<string, string>
                {
                    ["greetingName"] = "Jane Client",
                    ["whenLine"] = "Thu 26 Mar 2026 09:00 (Europe/London) to Thu 26 Mar 2026 10:00 (Europe/London)",
                    ["adviserName"] = "Alex Adviser",
                    ["locationLine"] = "Remote meeting",
                    ["note"] = "Your booking has been moved to the selected time.",
                    ["manageBookingLinks"] = BuildGenericManageLinks(links)
                }),
            CancellationToken.None);

        var content = Assert.Single(rendered.ChannelContent);
        Assert.Equal(NotificationChannel.Email, content.Channel);
        Assert.Equal(existing.Subject, content.Subject);
        Assert.Equal(existing.TextBody, content.TextBody);
        Assert.Null(content.HtmlBody);
    }

    [Fact]
    public async Task RenderAsync_BookingCancelledVersionedTemplate_MatchesExistingGenericTextOutput()
    {
        var links = CreateLinks();
        var existing = BookingNotificationEmailTemplate.Build(
            eventType: "Cancelled",
            clientDisplayName: "Jane Client",
            adviserName: "Alex Adviser",
            startUtc: new DateTime(2026, 03, 26, 9, 0, 0, DateTimeKind.Utc),
            endUtc: new DateTime(2026, 03, 26, 10, 0, 0, DateTimeKind.Utc),
            timezoneId: "Europe/London",
            isRemote: true,
            customMessage: "Please contact us if you need help rearranging.",
            viewUrl: links.ViewBookingUrl,
            cancelUrl: links.CancelBookingUrl,
            rescheduleUrl: links.RescheduleBookingUrl);

        var renderer = CreateRenderer();
        var rendered = await renderer.RenderAsync(
            new NotificationRequested(
                BookingNotificationTypes.BookingCancelled,
                "booking-1",
                new NotificationActor(LifecycleActors.Client, "Booking", null, null, null),
                [],
                new Dictionary<string, string>
                {
                    ["greetingName"] = "Jane Client",
                    ["whenLine"] = "Thu 26 Mar 2026 09:00 (Europe/London) to Thu 26 Mar 2026 10:00 (Europe/London)",
                    ["adviserName"] = "Alex Adviser",
                    ["locationLine"] = "Remote meeting",
                    ["note"] = "Please contact us if you need help rearranging.",
                    ["manageBookingLinks"] = BuildGenericManageLinks(links)
                }),
            CancellationToken.None);

        var content = Assert.Single(rendered.ChannelContent);
        Assert.Equal(NotificationChannel.Email, content.Channel);
        Assert.Equal(existing.Subject, content.Subject);
        Assert.Equal(existing.TextBody, content.TextBody);
        Assert.Null(content.HtmlBody);
    }

    [Fact]
    public async Task RenderAsync_BookingHoldVersionedTemplate_MatchesExistingHoldTextOutput()
    {
        var now = new DateTime(2026, 03, 26, 10, 0, 0, DateTimeKind.Utc);
        var transaction = CreateTransaction(now, isRemote: false);
        var slot = CreateSlot("slot-1", transaction.Id, now.AddHours(2), now.AddHours(3));
        var hold = BookingHold.Create(slot.Id, slot.AdviserId, TimeSpan.FromMinutes(10), now);
        var windows = new HoldWindows(slot.StartUtc.AddMinutes(-30), slot.EndUtc.AddMinutes(15), 15, 15, true);
        var links = CreateLinks();

        var existing = HoldBookingTemplate.BuildHoldTemplate(slot, transaction, hold, windows, links);

        var renderer = CreateRenderer();
        var rendered = await renderer.RenderAsync(
            new NotificationRequested(
                BookingNotificationTypes.BookingHoldCreated,
                hold.Id,
                new NotificationActor(LifecycleActors.Client, "Booking", null, null, null),
                [],
                new Dictionary<string, string>
                {
                    ["transactionRef"] = transaction.TransactionRef,
                    ["holdId"] = hold.Id,
                    ["adviserName"] = slot.AdviserName,
                    ["meetingType"] = transaction.MeetingType ?? "N/A",
                    ["when"] = "2026-03-26 12:00 (Europe/London) -> 2026-03-26 13:00 (Europe/London)",
                    ["holdExpires"] = "2026-03-26 10:10Z",
                    ["travelLine"] = "Travel time: 15 mins before",
                    ["companyLine"] = "Company buffer: 15 mins (pre/post meeting policy)",
                    ["manageBookingLinks"] = BuildHoldManageLinks(links)
                }),
            CancellationToken.None);

        var content = Assert.Single(rendered.ChannelContent);
        Assert.Equal(NotificationChannel.Email, content.Channel);
        Assert.Equal(existing.Subject, content.Subject);
        Assert.Equal(existing.TextBody, content.TextBody);
        Assert.Null(content.HtmlBody);
    }

    [Fact]
    public async Task RenderAsync_UsesDbTemplateWhenPresent()
    {
        var renderer = new NotificationTemplateRenderer(
            [new BookingNotificationTemplatePolicy()],
            new StubTemplateStore(new NotificationTemplateDefinition(
                "booking-confirmed",
                "v1",
                NotificationChannel.Email,
                "DB confirmed",
                null,
                "DB subject {{bookingId}}",
                "DB body {{bookingId}}",
                "text/plain",
                true)));

        var rendered = await renderer.RenderAsync(CreateExplicitTemplateRequest("booking-confirmed", "v1"), CancellationToken.None);

        var content = Assert.Single(rendered.ChannelContent);
        Assert.Equal("DB subject booking-1", content.Subject);
        Assert.Equal("DB body booking-1", content.TextBody);
    }

    [Fact]
    public async Task RenderAsync_UsesFileFallbackOnlyWhenDbTemplateIsMissing()
    {
        var renderer = new NotificationTemplateRenderer(
            [new BookingNotificationTemplatePolicy()],
            new StubTemplateStore((NotificationTemplateDefinition?)null));

        var rendered = await renderer.RenderAsync(CreateExplicitTemplateRequest("booking-confirmed", "v1"), CancellationToken.None);

        var content = Assert.Single(rendered.ChannelContent);
        Assert.Equal("AFH Booking: Booking Confirmed", content.Subject);
        Assert.Contains("Booking ID: booking-1", content.TextBody);
        Assert.Equal("text/html", content.ContentType);
        Assert.NotNull(content.HtmlBody);
        Assert.Contains("<a href=\"https://client.example/bookings/booking-1?token=token\">View booking</a>", content.HtmlBody);
        Assert.Contains("<a href=\"https://client.example/bookings/booking-1/cancel?token=token\">Cancel booking</a>", content.HtmlBody);
        Assert.Contains("<a href=\"https://client.example/bookings/booking-1/reschedule?token=token\">Reschedule booking</a>", content.HtmlBody);
    }

    [Fact]
    public async Task RenderAsync_MissingDbAndFileTemplateFailsClearly()
    {
        var renderer = new NotificationTemplateRenderer(
            [new BookingNotificationTemplatePolicy()],
            new StubTemplateStore((NotificationTemplateDefinition?)null));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            renderer.RenderAsync(CreateExplicitTemplateRequest("missing-template", "v9"), CancellationToken.None));

        Assert.Contains("missing-template", ex.Message);
    }

    [Fact]
    public async Task RenderAsync_ResolvesTemplateVersionPerChannel()
    {
        var renderer = new NotificationTemplateRenderer(
            [new BookingNotificationTemplatePolicy()],
            new StubTemplateStore(new NotificationTemplateDefinition(
                "booking-confirmed-sms",
                "v1",
                NotificationChannel.Sms,
                "DB confirmed SMS",
                null,
                "SMS subject",
                "SMS body {{bookingId}}",
                "text/plain",
                true)));

        var rendered = await renderer.RenderAsync(
            CreateExplicitTemplateRequest("booking-confirmed-sms", "v1", NotificationChannel.Sms),
            CancellationToken.None);

        var content = Assert.Single(rendered.ChannelContent);
        Assert.Equal(NotificationChannel.Sms, content.Channel);
        Assert.Equal("SMS body booking-1", content.TextBody);
        Assert.Null(content.HtmlBody);
        Assert.DoesNotContain("<a href=", content.TextBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenderAsync_ExplicitTemplate_RendersEmailAndSmsContent()
    {
        var renderer = new NotificationTemplateRenderer(
            [new BookingNotificationTemplatePolicy()],
            new StubTemplateStore(
            [
                new NotificationTemplateDefinition(
                    "booking-confirmed",
                    "v1",
                    NotificationChannel.Email,
                    "DB confirmed Email",
                    null,
                    "Email subject {{bookingId}}",
                    "Email body {{bookingId}}",
                    "text/plain",
                    true),
                new NotificationTemplateDefinition(
                    "booking-confirmed",
                    "v1",
                    NotificationChannel.Sms,
                    "DB confirmed SMS",
                    null,
                    null,
                    "SMS body {{bookingId}}",
                    "text/plain",
                    true)
            ]));

        var rendered = await renderer.RenderAsync(
            CreateExplicitTemplateRequest("booking-confirmed", "v1") with
            {
                Recipients =
                [
                    new NotificationRecipient("Client", "Jane Client", "jane@example.com", null, null, [NotificationChannel.Email]),
                    new NotificationRecipient("Client", "Jane Client", null, "+447700900000", null, [NotificationChannel.Sms])
                ]
            },
            CancellationToken.None);

        Assert.Equal(2, rendered.ChannelContent.Count);
        Assert.Contains(rendered.ChannelContent, x => x.Channel == NotificationChannel.Email && x.Subject == "Email subject booking-1" && x.TextBody == "Email body booking-1");
        Assert.Contains(rendered.ChannelContent, x => x.Channel == NotificationChannel.Sms && x.Subject is null && x.TextBody == "SMS body booking-1");
    }

    private static BookingSelfServiceLinks CreateLinks()
        => new(
            "https://client.example/bookings/booking-1?token=token",
            "https://client.example/bookings/booking-1/cancel?token=token",
            "https://client.example/bookings/booking-1/reschedule?token=token");

    private static string BuildGenericManageLinks(BookingSelfServiceLinks links)
        =>
$@"
Manage your booking:
- View: {links.ViewBookingUrl}
- Cancel: {links.CancelBookingUrl}
- Reschedule: {links.RescheduleBookingUrl}";

    private static string BuildHoldManageLinks(BookingSelfServiceLinks links)
        =>
$@"
Manage your booking:
- View booking: {links.ViewBookingUrl}
- Cancel booking: {links.CancelBookingUrl}
- Reschedule booking: {links.RescheduleBookingUrl}";

    private static NotificationTemplateRenderer CreateRenderer()
        => new([new BookingNotificationTemplatePolicy()]);

    private static NotificationRequested CreateExplicitTemplateRequest(
        string templateKey,
        string templateVersion,
        NotificationChannel channel = NotificationChannel.Email)
        => new(
            BookingNotificationTypes.BookingConfirmed,
            "booking-1",
            new NotificationActor(LifecycleActors.Client, "Booking", null, null, null),
            [new NotificationRecipient("Client", "Jane Client", channel == NotificationChannel.Email ? "jane@example.com" : null, channel == NotificationChannel.Sms ? "+447700900000" : null, null, [channel])],
            new Dictionary<string, string>
            {
                ["TemplateKey"] = templateKey,
                ["TemplateVersion"] = templateVersion,
                ["bookingId"] = "booking-1",
                ["transactionRef"] = "TRX-1",
                ["adviserName"] = "Alex Adviser",
                ["meetingType"] = "Review",
                ["when"] = "Thu 26 Mar 2026",
                ["whereLine"] = "",
                ["travelLine"] = "",
                ["manageBookingLinks"] = "",
                ["viewBookingUrl"] = "https://client.example/bookings/booking-1?token=token",
                ["cancelBookingUrl"] = "https://client.example/bookings/booking-1/cancel?token=token",
                ["rescheduleBookingUrl"] = "https://client.example/bookings/booking-1/reschedule?token=token"
            });

    private sealed class StubTemplateStore : INotificationTemplateStore
    {
        private readonly IReadOnlyCollection<NotificationTemplateDefinition> _templates;

        public StubTemplateStore(NotificationTemplateDefinition? template)
        {
            _templates = template is null ? [] : [template];
        }

        public StubTemplateStore(IReadOnlyCollection<NotificationTemplateDefinition> templates)
        {
            _templates = templates;
        }

        public Task<NotificationTemplateDefinition?> GetAsync(
            string templateKey,
            string templateVersion,
            NotificationChannel channel,
            CancellationToken ct)
            => Task.FromResult(
                _templates.SingleOrDefault(template =>
                    template.TemplateKey == templateKey &&
                    template.TemplateVersion == templateVersion &&
                    template.Channel == channel));
    }

    private static BookingTransaction CreateTransaction(DateTime now, bool isRemote) =>
        BookingTransaction.Rehydrate(
            id: "tx-1",
            transactionRef: "TRX-1",
            proposedStartUtc: now.AddHours(2),
            duration: TimeSpan.FromHours(1),
            timezone: "Europe/London",
            isRemote: isRemote,
            meetingType: "Review",
            locationRef: null,
            status: BookingTransactionStatus.Open,
            createdUtc: now.AddHours(-1),
            expiresUtc: now.AddHours(1));

    private static BookingSlot CreateSlot(string id, string transactionId, DateTime startUtc, DateTime endUtc) =>
        BookingSlot.Rehydrate(
            id: id,
            transactionRef: transactionId,
            adviserId: "adv-1",
            adviserName: "Adviser One",
            startUtc: startUtc,
            endUtc: endUtc,
            score: 5,
            scoreBreakdown: null,
            locationRef: "loc-1",
            travelMinutes: 15,
            companyBufferMinutes: 15,
            distanceMiles: 10,
            travelStatus: "Eligible",
            travelMessage: null,
            createdUtc: startUtc.AddHours(-1));
}