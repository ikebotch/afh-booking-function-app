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
    public async Task RunAsync_DoesNotSendEmailSynchronously()
    {
        _ingestionMock.Setup(x => x.AcceptAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationRequestAcceptedResult(Guid.NewGuid(), "Accepted", "corr-123", true));
        var sut = new SubmitNotificationRequestFunction(_ingestionMock.Object);

        await sut.RunAsync(CreateJsonRequest(CreateRequest()), CancellationToken.None);

        _ingestionMock.Verify(x => x.AcceptAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static TestHttpRequestData CreateJsonRequest(NotificationRequested request)
    {
        var httpRequest = TestHttpRequestData.Create(method: "POST");
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var bytes = Encoding.UTF8.GetBytes(json);
        httpRequest.Body.Write(bytes, 0, bytes.Length);
        httpRequest.Body.Position = 0;
        return httpRequest;
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
