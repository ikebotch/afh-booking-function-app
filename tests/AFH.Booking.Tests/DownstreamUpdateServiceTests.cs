using AFH.Booking.Infrastructure.Clients;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using AFH.Booking.Domain.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;

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
            Options.Create(new XPlanOptions { Enabled = true, BaseUrl = "https://xplan.example", ApiKey = "token" }),
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
            Options.Create(new XPlanOptions { Enabled = true, BaseUrl = "https://xplan.example" }),
            NullLogger<DownstreamUpdateService>.Instance);

        var result = await sut.ReconcileAsync(10, 5, includePending: true, correlationId: null, CancellationToken.None);

        Assert.Equal(0, result.RequestedCount);
        var row = await db.DownstreamUpdates.SingleAsync();
        Assert.Equal("Pending", row.Status);
        Assert.Equal(1, row.AttemptCount);
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
