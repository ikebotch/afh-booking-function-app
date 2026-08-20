using AFH.Booking.Infrastructure.Clients;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using AFH.Booking.Domain.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace AFH.Booking.Tests;

public sealed class DownstreamUpdateServiceTests
{
    [Fact]
    public async Task ReconcileAsync_RetriesFailedRowsAndMarksSuccess()
    {
        await using var db = CreateDb();
        db.DownstreamUpdates.Add(new DownstreamUpdateModel
        {
            Id = "upd-1",
            BookingId = "booking-1",
            ChangeType = "Cancel",
            TransactionRef = "TRX-1",
            PayloadJson = "{\"bookingId\":\"booking-1\"}",
            Status = "Failed",
            AttemptCount = 1,
            CreatedUtc = DateTime.UtcNow.AddMinutes(-30),
            ProcessedUtc = DateTime.UtcNow.AddMinutes(-29),
            ErrorMessage = "timeout"
        });
        await db.SaveChangesAsync();

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var sut = new DownstreamUpdateService(
            db,
            CreateHttpClientFactory(handler),
            Options.Create(new PartnerWorkflowOptions { Enabled = true, BaseUrl = "https://partner.example", ApiKey = "token" }),
            NullLogger<DownstreamUpdateService>.Instance);

        var result = await sut.ReconcileAsync(10, 5, includePending: false, correlationId: "cid-1", CancellationToken.None);

        Assert.Equal(1, result.RequestedCount);
        Assert.Equal(1, result.SucceededCount);
        var row = await db.DownstreamUpdates.SingleAsync();
        Assert.Equal("Sent", row.Status);
        Assert.Equal(2, row.AttemptCount);
        Assert.Null(row.ErrorMessage);
    }

    [Fact]
    public async Task ReconcileAsync_LeavesRecentPendingRowsAlone()
    {
        await using var db = CreateDb();
        db.DownstreamUpdates.Add(new DownstreamUpdateModel
        {
            Id = "upd-2",
            BookingId = "booking-2",
            ChangeType = "Rearrange",
            TransactionRef = "TRX-2",
            PayloadJson = "{}",
            Status = "Pending",
            AttemptCount = 1,
            CreatedUtc = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var sut = new DownstreamUpdateService(
            db,
            CreateHttpClientFactory(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
            Options.Create(new PartnerWorkflowOptions { Enabled = true, BaseUrl = "https://partner.example" }),
            NullLogger<DownstreamUpdateService>.Instance);

        var result = await sut.ReconcileAsync(10, 5, includePending: true, correlationId: null, CancellationToken.None);

        Assert.Equal(0, result.RequestedCount);
        var row = await db.DownstreamUpdates.SingleAsync();
        Assert.Equal("Pending", row.Status);
        Assert.Equal(1, row.AttemptCount);
    }

    [Fact]
    public async Task PublishBookingChangeAsync_WhenPartnerWorkflowConfigured_SendsPartnerPayloadAndHeaders()
    {
        await using var db = CreateDb();
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var sut = new DownstreamUpdateService(
            db,
            CreateHttpClientFactory(handler),
            Options.Create(new PartnerWorkflowOptions
            {
                Enabled = true,
                BookingUpdatesUrl = "https://hooks.zapier.com/hooks/catch/2090738/44r9dzl",
                ApiKey = "test",
                ApiKeyHeaderName = "X-Api-Key",
                PayloadFormat = "PartnerWorkflow"
            }),
            NullLogger<DownstreamUpdateService>.Instance);

        var result = await sut.PublishBookingChangeAsync(
            bookingId: "32fa33614e1a49eeb6cd543d47da8afa",
            changeType: "Rearrange",
            transactionRef: "000123456",
            payloadJson: """
                {
                  "newStartUtc": "2026-08-27T14:00:00Z",
                  "meetingType": "Telephone",
                  "newAdviserId": "987654321",
                  "reasonDetail": "Reason for reschedule if collected",
                  "bookingReference": "000123456"
                }
                """,
            CancellationToken.None);

        Assert.Equal("Sent", result.Status);
        Assert.NotNull(capturedRequest);
        Assert.Equal("https://hooks.zapier.com/hooks/catch/2090738/44r9dzl", capturedRequest!.RequestUri!.ToString());
        Assert.Equal("test", capturedRequest.Headers.GetValues("X-Api-Key").Single());
        Assert.Equal(
            "booking-rescheduled:32fa33614e1a49eeb6cd543d47da8afa",
            capturedRequest.Headers.GetValues("X-Idempotency-Key").Single());

        Assert.NotNull(capturedBody);
        using var body = JsonDocument.Parse(capturedBody!);
        var root = body.RootElement;
        Assert.Equal("000123456", root.GetProperty("transactionId").GetString());
        Assert.Equal("Rescheduled", root.GetProperty("status").GetString());
        Assert.Equal("2026-08-27T14:00:00Z", root.GetProperty("dateTime").GetString());
        Assert.Equal("Telephone", root.GetProperty("meetingType").GetString());
        Assert.Equal("987654321", root.GetProperty("adviserId").GetString());
        Assert.Equal("Reason for reschedule if collected", root.GetProperty("notes").GetString());
        Assert.Equal("000123456", root.GetProperty("bookingReference").GetString());
    }

    private static BookingDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new BookingDbContext(options);
    }

    private static IHttpClientFactory CreateHttpClientFactory(HttpMessageHandler handler)
        => new StubHttpClientFactory(new HttpClient(handler));

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
