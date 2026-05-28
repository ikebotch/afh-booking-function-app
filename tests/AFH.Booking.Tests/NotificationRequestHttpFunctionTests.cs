using System.Text;
using System.Text.Json;
using System.Net;
using AFH.Booking.Function.Functions.V1.Notifications.Inbound;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;

namespace AFH.Booking.Tests;

public sealed class NotificationRequestHttpFunctionTests
{
    private readonly Mock<INotificationRequestIngestionService> _ingestionMock = new();

    [Fact]
    public async Task RunAsync_ValidRequest_ReturnsAcceptedResponse()
    {
        var outboxId = Guid.NewGuid();
        _ingestionMock.Setup(x => x.AcceptAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationRequestAcceptedResult(outboxId, "Accepted", "corr-123", true));
        var sut = new SubmitNotificationRequestFunction(_ingestionMock.Object);

        var response = await sut.RunAsync(CreateJsonRequest(CreateRequest()), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var json = await ReadBodyAsync(response);
        Assert.Contains(outboxId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Accepted", json, StringComparison.Ordinal);
        Assert.Contains("corr-123", json, StringComparison.Ordinal);
        _ingestionMock.Verify(x => x.AcceptAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_InvalidRequest_ReturnsBadRequest()
    {
        _ingestionMock.Setup(x => x.AcceptAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotificationRequestValidationException("SourceApplication is required."));
        var sut = new SubmitNotificationRequestFunction(_ingestionMock.Object);

        var response = await sut.RunAsync(CreateJsonRequest(CreateRequest()), CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("SourceApplication is required.", await ReadBodyAsync(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_FlatPayloadWithStringChannels_ReturnsAcceptedResponse()
    {
        var outboxId = Guid.NewGuid();
        NotificationRequested? capturedRequest = null;
        _ingestionMock.Setup(x => x.AcceptAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRequested, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new NotificationRequestAcceptedResult(outboxId, "Accepted", "corr-flat", true));
        var sut = new SubmitNotificationRequestFunction(_ingestionMock.Object);
        var request = CreateRawJsonRequest("""
            {
              "sourceApplication": "Booking",
              "notificationType": "BookingConfirmed",
              "sourceReferenceType": "Booking",
              "sourceReferenceId": "booking-123",
              "idempotencyKey": "booking-confirmed:booking-123",
              "correlationId": "corr-flat",
              "templateKey": "booking-confirmed",
              "templateVersion": "v1",
              "channels": ["Email"],
              "recipients": [
                {
                  "recipientType": "Client",
                  "displayName": "Client",
                  "email": "client@example.com"
                }
              ],
              "data": {
                "bookingId": "booking-123"
              }
            }
            """);

        var response = await sut.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(capturedRequest);
        Assert.Equal("Booking", capturedRequest!.Type.SourceApplication);
        Assert.Equal("BookingConfirmed", capturedRequest.Type.Name);
        Assert.Equal("corr-flat", capturedRequest.CorrelationId);
        Assert.Equal("booking-confirmed", capturedRequest.Data["TemplateKey"]);
        Assert.Equal("v1", capturedRequest.Data["TemplateVersion"]);
        Assert.Equal("Booking", capturedRequest.Data["SourceReferenceType"]);
        Assert.Equal("booking-123", capturedRequest.Data["SourceReferenceId"]);
        Assert.Equal("booking-confirmed:booking-123", capturedRequest.Data["IdempotencyKey"]);
        Assert.Equal([NotificationChannel.Email], capturedRequest.Recipients[0].PreferredChannels);
    }

    [Fact]
    public async Task RunAsync_MissingType_ReturnsBadRequest()
    {
        _ingestionMock.Setup(x => x.AcceptAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotificationRequestValidationException("Notification type is required."));
        var sut = new SubmitNotificationRequestFunction(_ingestionMock.Object);
        var request = CreateRawJsonRequest("""
            {
              "correlationId": "corr-123",
              "actor": {
                "actorType": "System",
                "sourceApplication": "Booking"
              },
              "recipients": [
                {
                  "recipientType": "Client",
                  "displayName": "Client",
                  "email": "client@example.com"
                }
              ],
              "data": {
                "TemplateKey": "booking-confirmed",
                "TemplateVersion": "v1"
              }
            }
            """);

        var response = await sut.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Notification type is required.", await ReadBodyAsync(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_MissingBody_ReturnsBadRequest()
    {
        var sut = new SubmitNotificationRequestFunction(_ingestionMock.Object);
        var request = TestHttpRequestData.Create(method: "POST");

        var response = await sut.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _ingestionMock.Verify(x => x.AcceptAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_MalformedFlattenedPayload_ReturnsBadRequest()
    {
        _ingestionMock.Setup(x => x.AcceptAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotificationRequestValidationException("At least one recipient is required."));
        var sut = new SubmitNotificationRequestFunction(_ingestionMock.Object);
        var request = CreateRawJsonRequest("""
            {
              "sourceApplication": "Booking",
              "notificationType": "BookingConfirmed",
              "sourceReferenceType": "Booking",
              "sourceReferenceId": "booking-123",
              "idempotencyKey": "booking-confirmed:booking-123",
              "templateKey": "booking-confirmed",
              "templateVersion": "v1"
            }
            """);

        var response = await sut.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("At least one recipient is required.", await ReadBodyAsync(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_FlatPayloadWithInvalidChannel_ReturnsBadRequest()
    {
        var sut = new SubmitNotificationRequestFunction(_ingestionMock.Object);
        var request = CreateRawJsonRequest("""
            {
              "sourceApplication": "Booking",
              "notificationType": "BookingConfirmed",
              "correlationId": "corr-flat",
              "templateKey": "booking-confirmed",
              "templateVersion": "v1",
              "channels": ["Fax"],
              "recipients": [
                {
                  "recipientType": "Client",
                  "displayName": "Client",
                  "email": "client@example.com"
                }
              ]
            }
            """);

        var response = await sut.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("channels must contain only Email, Sms or Push.", await ReadBodyAsync(response), StringComparison.Ordinal);
        _ingestionMock.Verify(x => x.AcceptAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_DoesNotSendEmailSynchronously()
    {
        _ingestionMock.Setup(x => x.AcceptAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationRequestAcceptedResult(Guid.NewGuid(), "Accepted", "corr-123", true));
        var sut = new SubmitNotificationRequestFunction(_ingestionMock.Object);

        await sut.RunAsync(CreateJsonRequest(CreateRequest()), CancellationToken.None);

        _ingestionMock.Verify(x => x.AcceptAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static TestHttpRequestData CreateJsonRequest(object request)
    {
        var httpRequest = TestHttpRequestData.Create(method: "POST");
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        WriteBody(httpRequest, json);
        return httpRequest;
    }

    private static TestHttpRequestData CreateRawJsonRequest(string json)
    {
        var httpRequest = TestHttpRequestData.Create(method: "POST");
        WriteBody(httpRequest, json);
        return httpRequest;
    }

    private static void WriteBody(TestHttpRequestData httpRequest, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        httpRequest.Body.Write(bytes, 0, bytes.Length);
        httpRequest.Body.Position = 0;
    }

    private static async Task<string> ReadBodyAsync(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static NotificationRequested CreateRequest()
        => new(
            new NotificationType("Booking", "BookingConfirmed"),
            "corr-123",
            new NotificationActor("System", "Booking", null, null, null),
            [new NotificationRecipient("Client", "Client", "client@example.com", null, null, [NotificationChannel.Email])],
            new Dictionary<string, string> { ["TemplateKey"] = "booking-confirmed", ["TemplateVersion"] = "v1" });
}
