using AFH.Notification.Infrastructure.Bouncebacks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AFH.Booking.Tests;

public class EmailBouncebackParserTests
{
    private readonly EmailBouncebackParser _sut;

    public EmailBouncebackParserTests()
    {
        _sut = new EmailBouncebackParser(NullLogger<EmailBouncebackParser>.Instance);
    }

    [Fact]
    public void Parse_WhenValidationEvent_ReturnsValidationResponse()
    {
        var payload = @"[{
            ""id"": ""1"",
            ""eventType"": ""Microsoft.EventGrid.SubscriptionValidationEvent"",
            ""data"": {
                ""validationCode"": ""test-code-123""
            }
        }]";

        var (result, bouncebacks) = _sut.Parse(payload);

        Assert.True(result.IsSuccess);
        Assert.Equal("test-code-123", result.ValidationResponse);
        Assert.Empty(bouncebacks);
    }

    [Fact]
    public void Parse_WhenDeliveryReportReceived_ReturnsBounceback()
    {
        var payload = @"[{
            ""id"": ""2"",
            ""eventType"": ""Microsoft.Communication.EmailDeliveryReportReceived"",
            ""eventTime"": ""2026-05-27T10:00:00Z"",
            ""data"": {
                ""messageId"": ""msg-123"",
                ""status"": ""Bounced"",
                ""deliveryStatusDetails"": {
                    ""statusMessage"": ""5.1.1 User unknown""
                }
            }
        }]";

        var (result, bouncebacks) = _sut.Parse(payload);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.ProcessedCount);
        Assert.Single(bouncebacks);
        
        var bounce = bouncebacks[0];
        Assert.Equal("msg-123", bounce.ProviderMessageId);
        Assert.Equal("Bounced", bounce.Status);
        Assert.Equal("5.1.1 User unknown", bounce.BounceReason);
        Assert.Equal(new DateTime(2026, 5, 27, 10, 0, 0, DateTimeKind.Utc), bounce.TimestampUtc);
    }
}
