using System.Threading;
using System.Threading.Tasks;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Infrastructure.Bouncebacks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AFH.Booking.Tests;

public class EmailBouncebackProcessorTests
{
    private readonly Mock<INotificationBouncebackStore> _storeMock;
    private readonly EmailBouncebackProcessor _sut;

    public EmailBouncebackProcessorTests()
    {
        _storeMock = new Mock<INotificationBouncebackStore>();
        var parser = new EmailBouncebackParser(NullLogger<EmailBouncebackParser>.Instance);
        _sut = new EmailBouncebackProcessor(parser, _storeMock.Object, NullLogger<EmailBouncebackProcessor>.Instance);
    }

    [Fact]
    public async Task ProcessWebhookPayloadAsync_WithValidBounceback_CallsStore()
    {
        var payload = @"[{
            ""id"": ""2"",
            ""eventType"": ""Microsoft.Communication.EmailDeliveryReportReceived"",
            ""eventTime"": ""2026-05-27T10:00:00Z"",
            ""data"": {
                ""messageId"": ""msg-123"",
                ""status"": ""Bounced""
            }
        }]";

        var result = await _sut.ProcessWebhookPayloadAsync(payload, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.ProcessedCount);
        
        _storeMock.Verify(s => s.RecordBouncebackAsync(
            It.Is<NotificationBounceback>(b => b.ProviderMessageId == "msg-123" && b.Status == "Bounced"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
