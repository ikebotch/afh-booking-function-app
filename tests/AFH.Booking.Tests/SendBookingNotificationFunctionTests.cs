using System.Text;
using AFH.Booking.Application.Abstractions.Notifications;
using AFH.Booking.Application.Models.Notifications;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Function.Functions.V1.Bookings;
using System.Net;

namespace AFH.Booking.Tests;

public sealed class SendBookingNotificationFunctionTests
{
    [Fact]
    public async Task Run_UsesManualOutboxNotificationService()
    {
        var service = new StubManualBookingNotificationService();
        var sut = new SendBookingNotificationFunction(service);
        var request = CreateJsonRequest("""{"eventType":"Booked","sendSms":false,"sendEmail":true}""");
        request.Headers.Add("x-correlation-id", "corr-1");

        var response = await sut.Run(request, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("booking-1", service.LastBookingId);
        Assert.Equal("Booked", service.LastEventType);
        Assert.False(service.LastSendSms);
        Assert.True(service.LastSendEmail);
        Assert.Equal("corr-1", service.LastCorrelationId);
    }

    [Fact]
    public async Task Run_ReturnsBadRequestForServiceValidationFailure()
    {
        var service = new StubManualBookingNotificationService
        {
            Result = Result<NotificationDispatchResponse>.Fail(HttpStatusCode.BadRequest, "Unsupported EventType.", Errors.Validation)
        };
        var sut = new SendBookingNotificationFunction(service);
        var request = CreateJsonRequest("""{"eventType":"BookingChanged","sendSms":false,"sendEmail":true}""");

        var response = await sut.Run(request, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static TestHttpRequestData CreateJsonRequest(string json)
    {
        var request = TestHttpRequestData.Create(method: "POST");
        using var writer = new StreamWriter(request.Body, Encoding.UTF8, leaveOpen: true);
        writer.Write(json);
        writer.Flush();
        request.Body.Position = 0;
        return request;
    }

    private sealed class StubManualBookingNotificationService : IManualBookingNotificationService
    {
        public string? LastBookingId { get; private set; }
        public string? LastEventType { get; private set; }
        public bool LastSendSms { get; private set; }
        public bool LastSendEmail { get; private set; }
        public string? LastCorrelationId { get; private set; }

        public Result<NotificationDispatchResponse> Result { get; init; } = Result<NotificationDispatchResponse>.Ok(new NotificationDispatchResponse
        {
            DispatchId = "corr-1",
            BookingId = "booking-1",
            EventType = "BookingConfirmed",
            SmsRequested = false,
            EmailRequested = true,
            SmsStatus = "Skipped",
            EmailStatus = "Queued",
            CreatedUtc = DateTime.UtcNow
        });

        public Task<Result<NotificationDispatchResponse>> SendAsync(
            string bookingId,
            string eventType,
            string? messageOverride,
            bool sendSms,
            bool sendEmail,
            string? correlationId,
            CancellationToken ct)
        {
            LastBookingId = bookingId;
            LastEventType = eventType;
            LastSendSms = sendSms;
            LastSendEmail = sendEmail;
            LastCorrelationId = correlationId;
            return Task.FromResult(Result);
        }
    }
}
