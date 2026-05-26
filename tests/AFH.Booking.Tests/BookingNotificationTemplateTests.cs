using AFH.Booking.Application.EmailTemplates;
using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Calendar;

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
        Assert.Contains("View Booking", template.HtmlBody);
        Assert.Contains("Reschedule", template.HtmlBody);
        Assert.Contains("Cancel", template.HtmlBody);
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
        Assert.Contains("View booking", template.HtmlBody);
        Assert.Contains("Cancel booking", template.HtmlBody);
        Assert.Contains("Reschedule booking", template.HtmlBody);
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
        Assert.Contains("View booking", template.HtmlBody);
        Assert.Contains("Cancel booking", template.HtmlBody);
        Assert.Contains("Reschedule booking", template.HtmlBody);
        Assert.Contains("View booking: https://client.example/bookings/booking-1?token=token", template.CalendarDescription);
        Assert.DoesNotContain("<html", template.CalendarDescription, StringComparison.OrdinalIgnoreCase);
    }

    private static BookingSelfServiceLinks CreateLinks()
        => new(
            "https://client.example/bookings/booking-1?token=token",
            "https://client.example/bookings/booking-1/cancel?token=token",
            "https://client.example/bookings/booking-1/reschedule?token=token");

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
