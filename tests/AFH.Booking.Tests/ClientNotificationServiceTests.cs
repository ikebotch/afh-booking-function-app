using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Client;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Clients;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AFH.Booking.Tests;

public sealed class ClientNotificationServiceTests
{
    [Fact]
    public async Task SendBookingNotificationAsync_SendsHtmlAndTextBodiesSeparately()
    {
        var hold = BookingHold.Rehydrate("hold-1", "slot-1", "client-1", BookingHoldStatus.Confirmed, System.DateTime.UtcNow, System.DateTime.UtcNow.AddHours(1), null, null, null, null, null, null);

        var slot = BookingSlot.Rehydrate(
            id: "slot-1",
            transactionRef: "tx-1",
            adviserId: "adv-1",
            adviserName: "Adviser One",
            startUtc: new DateTime(2026, 03, 27, 11, 0, 0, DateTimeKind.Utc),
            endUtc: new DateTime(2026, 03, 27, 12, 0, 0, DateTimeKind.Utc),
            score: 5,
            scoreBreakdown: null,
            locationRef: null,
            travelMinutes: 0,
            companyBufferMinutes: 0,
            distanceMiles: null,
            travelStatus: null,
            travelMessage: null,
            createdUtc: new DateTime(2026, 03, 26, 9, 0, 0, DateTimeKind.Utc));

        var transaction = BookingTransaction.Rehydrate(
            id: "tx-1",
            transactionRef: "TRX-1",
            proposedStartUtc: slot.StartUtc,
            duration: TimeSpan.FromHours(1),
            timezone: "Europe/London",
            isRemote: true,
            meetingType: "Review",
            locationRef: null,
            status: BookingTransactionStatus.Open,
            createdUtc: new DateTime(2026, 03, 26, 9, 0, 0, DateTimeKind.Utc),
            expiresUtc: new DateTime(2026, 03, 27, 9, 0, 0, DateTimeKind.Utc));

        var holds = new Mock<IBookingHoldRepository>();
        holds.Setup(x => x.GetAsync("hold-1", It.IsAny<CancellationToken>())).ReturnsAsync(hold);

        var slots = new Mock<IBookingSlotRepository>();
        slots.Setup(x => x.GetAsync("slot-1", It.IsAny<CancellationToken>())).ReturnsAsync(slot);

        var transactions = new Mock<IBookingTransactionRepository>();
        transactions.Setup(x => x.GetAsync("tx-1", It.IsAny<CancellationToken>())).ReturnsAsync(transaction);

        var clients = new Mock<IClientDirectory>();
        clients.Setup(x => x.GetAsync("TRX-1", It.IsAny<CancellationToken>())).ReturnsAsync(new ClientDirectoryItem
        {
            FirstName = "Jane",
            LastName = "Client",
            Email = "jane@example.com",
            Phone = "07123456789"
        });

        var emailSender = new Mock<IEmailNotificationSender>();
        EmailNotificationMessage? capturedMessage = null;
        emailSender
            .Setup(x => x.SendAsync(It.IsAny<EmailNotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailNotificationMessage, CancellationToken>((message, _) => capturedMessage = message)
            .ReturnsAsync(new EmailNotificationSendResult("Sent", "provider-1"));

        var dispatches = new Mock<INotificationDispatchRepository>();
        NotificationDispatchRecord? persistedRecord = null;
        dispatches
            .Setup(x => x.AddAsync(It.IsAny<NotificationDispatchRecord>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationDispatchRecord, CancellationToken>((record, _) => persistedRecord = record)
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new ClientNotificationService(
            holds.Object,
            slots.Object,
            transactions.Object,
            clients.Object,
            emailSender.Object,
            dispatches.Object,
            unitOfWork.Object,
            new HttpClientFactoryStub(),
            Options.Create(new NotificationsOptions
            {
                EmailEnabled = true,
                SmsEnabled = false
            }),
            NullLogger<ClientNotificationService>.Instance);

        var response = await service.SendBookingNotificationAsync(
            bookingId: "hold-1",
            eventType: "Confirmed",
            message: "Your appointment is confirmed.",
            sendSms: false,
            sendEmail: true,
            ct: CancellationToken.None);

        Assert.NotNull(capturedMessage);
        Assert.Contains("<html", capturedMessage!.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<html", capturedMessage.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("jane@example.com", capturedMessage.RecipientEmail);

        Assert.NotNull(persistedRecord);
        Assert.DoesNotContain("<html", persistedRecord!.MessageBody ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Sent", response.EmailStatus);
    }

    private sealed class HttpClientFactoryStub : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
