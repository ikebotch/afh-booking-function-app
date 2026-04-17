using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Client;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Location.Travel;
using AFH.Booking.Domain.Options;
using AFH.Booking.Domain.Transactions;
using AFH.Booking.Infrastructure.Auth;
using AFH.Booking.Infrastructure.Calendar;
using AFH.Booking.Infrastructure.Location;
using AFH.Booking.Infrastructure.Meetings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace AFH.Booking.Tests;

public class InternalOutboundAuthTests
{
    [Fact]
    public async Task CalendarGateway_UsesFunctionKeyAndBearerAuth_WithoutCodeQuery()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":{\"appointmentId\":\"appt-1\"}}", Encoding.UTF8, "application/json")
            };
        });

        var sut = new CalendarGateway(
            new HttpClient(handler),
            Options.Create(new CalendarSubscriptionOptions
            {
                BaseUrl = "https://calendar.example",
                FunctionKey = "calendar-function-key",
                InternalToken = "calendar-token"
            }),
            new InternalBearerServiceAuthenticator(),
            NullLogger<CalendarGateway>.Instance);

        var result = await sut.CreateBookingEventAsync(
            BookingCalendarEvent.Create(
                userId: "adv-1",
                externalId: "booking-1",
                subject: "AFH Booking",
                startUtc: DateTime.UtcNow,
                endUtc: DateTime.UtcNow.AddHours(1),
                timezone: "UTC",
                isRemote: true,
                categories: ["AFH Booking"],
                body: "body",
                providerEventId: null,
                location: null,
                attendees: [],
                showAs: BookingShowAs.Busy),
            CancellationToken.None);

        Assert.Equal("appt-1", result);
        Assert.NotNull(captured);
        Assert.True(captured!.Headers.TryGetValues("x-functions-key", out var functionKeyValues));
        Assert.Equal("calendar-function-key", functionKeyValues!.Single());
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("calendar-token", captured.Headers.Authorization?.Parameter);
        Assert.DoesNotContain("code=", captured.RequestUri!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TravelMatrixService_UsesFunctionKeyAndBearerAuth_WithoutCodeQuery()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":{\"candidates\":[]}}", Encoding.UTF8, "application/json")
            };
        });

        var sut = new TravelMatrixService(
            new HttpClient(handler),
            Options.Create(new LocationServiceOptions
            {
                BaseUrl = "https://location.example",
                FunctionKey = "location-function-key",
                InternalToken = "location-token"
            }),
            new InternalBearerServiceAuthenticator(),
            NullLogger<TravelMatrixService>.Instance);

        await sut.GetAsync(new TravelMatrixRequest
        {
            RequestId = "req-1"
        }, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.True(captured!.Headers.TryGetValues("x-functions-key", out var functionKeyValues));
        Assert.Equal("location-function-key", functionKeyValues!.Single());
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("location-token", captured.Headers.Authorization?.Parameter);
        Assert.DoesNotContain("code=", captured.RequestUri!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TravelMatrixService_PreservesUnverifiedTravelValuesFromLocationResponse()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"data\":{\"candidates\":[{\"adviserId\":\"adv-1\",\"mailboxUserId\":\"adviser.one@tenant.com\",\"goldStar\":false,\"travelToClient\":{\"etaMinutes\":null,\"distanceMiles\":null,\"confidence\":\"Low\"},\"buffers\":{\"companyBufferMinutes\":30}}]}}",
                Encoding.UTF8,
                "application/json")
        });

        var sut = new TravelMatrixService(
            new HttpClient(handler),
            Options.Create(new LocationServiceOptions
            {
                BaseUrl = "https://location.example",
                FunctionKey = "location-function-key",
                InternalToken = "location-token"
            }),
            new InternalBearerServiceAuthenticator(),
            NullLogger<TravelMatrixService>.Instance);

        var result = await sut.GetAsync(new TravelMatrixRequest
        {
            RequestId = "req-1"
        }, CancellationToken.None);

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal("adv-1", candidate.AdviserId);
        Assert.Null(candidate.TravelMinutes);
        Assert.Null(candidate.DistanceMiles);
        Assert.Equal(30, candidate.CompanyBufferMinutes);
    }

    [Fact]
    public async Task AcsMeetingLinkFactory_UsesFunctionKeyAndBearerAuth()
    {
        HttpRequestMessage? captured = null;
        string? capturedJsonBody = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            if (request.Content is not null)
                capturedJsonBody = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"meetingId\":\"m-1\",\"groupId\":\"g-1\",\"joinCode\":\"g-1\",\"clientJoinUrl\":\"https://acs.example/meeting/abc?role=client\",\"adviserJoinUrl\":\"https://acs.example/meeting/abc?role=adviser\"}",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var sut = new AcsMeetingLinkFactory(
            new HttpClient(handler) { BaseAddress = new Uri("https://acs.example") },
            Options.Create(new AcsOptions
            {
                Enabled = true,
                MeetingLinkServiceBaseUrl = "https://acs.example",
                FunctionKey = "acs-function-key",
                InternalToken = "acs-internal-token"
            }),
            NullLogger<AcsMeetingLinkFactory>.Instance,
            holds: new StubHoldRepo(),
            slots: new StubSlotRepo(),
            tx: new StubTxRepo(),
            clients: new StubClientDirectory());

        var result = await sut.CreateJoinLinkAsync("booking-123", CancellationToken.None);

        Assert.Equal("https://acs.example/meeting/abc?role=client", result);
        Assert.NotNull(captured);
        Assert.True(captured!.Headers.TryGetValues("x-functions-key", out var functionKeyValues));
        Assert.Equal("acs-function-key", functionKeyValues!.Single());
        Assert.Equal("Bearer", captured.Headers.Authorization?.Scheme);
        Assert.Equal("acs-internal-token", captured.Headers.Authorization?.Parameter);
        Assert.Equal("/api/v1/meet/create", captured.RequestUri!.AbsolutePath);

        Assert.False(string.IsNullOrWhiteSpace(capturedJsonBody));
        using var doc = System.Text.Json.JsonDocument.Parse(capturedJsonBody!);
        var root = doc.RootElement;
        Assert.Equal("adv-1", root.GetProperty("adviserId").GetString());
        Assert.Equal("lead-1", root.GetProperty("leadId").GetString());
        Assert.Equal("Review", root.GetProperty("meetingType").GetString());
        Assert.Equal("AFH Booking - Review", root.GetProperty("title").GetString());
        Assert.Equal("client@example.com", root.GetProperty("clientEmail").GetString());
        Assert.Equal("Client Example", root.GetProperty("clientName").GetString());
    }

    private sealed class StubHoldRepo : IBookingHoldRepository
    {
        public Task AddAsync(BookingHold hold, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(BookingHold hold, CancellationToken ct) => throw new NotSupportedException();
        public Task<BookingHold?> GetForUpdateAsync(string holdId, CancellationToken ct) => throw new NotSupportedException();
        public Task<BookingHold?> GetBySlotIdAsync(string slotId, CancellationToken ct) => throw new NotSupportedException();
        public Task<BookingHold?> GetByCalendarEventIdAsync(string providerEventId, CancellationToken ct) => throw new NotSupportedException();
        public Task<BookingHold?> GetActiveBySlotIdAsync(string slotId, DateTime utcNow, CancellationToken ct) => throw new NotSupportedException();
        public Task<BookingHold?> GetActiveByTransactionIdAsync(string transactionId, DateTime utcNow, CancellationToken ct) => throw new NotSupportedException();
        public Task<ActiveHoldLookupResult> GetActiveForCreateHoldAsync(string transactionId, string slotId, DateTime utcNow, CancellationToken ct) => throw new NotSupportedException();
        public Task<BookingHold?> GetTrackedAsync(string holdId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<BookingHold>> GetExpiredActiveAsync(DateTime utcNow, int take, CancellationToken ct) => throw new NotSupportedException();

        public Task<BookingHold?> GetAsync(string holdId, CancellationToken ct)
            => Task.FromResult<BookingHold?>(BookingHold.Rehydrate(
                id: holdId,
                slotId: "slot-1",
                userid: "calendar-user-1",
                status: BookingHoldStatus.Active,
                createdUtc: DateTime.UtcNow,
                expiresUtc: DateTime.UtcNow.AddMinutes(10),
                confirmedUtc: null,
                releasedUtc: null,
                cancelledUtc: null,
                cancelReason: null,
                providerEventId: null));
    }

    private sealed class StubSlotRepo : IBookingSlotRepository
    {
        public Task AddRangeAsync(IEnumerable<BookingSlot> slots, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<BookingSlot>> ListByTransactionAsync(string transactionId, CancellationToken ct) => throw new NotSupportedException();
        public Task AddAsync(BookingSlot slot, CancellationToken ct) => throw new NotSupportedException();

        public Task<BookingSlot?> GetAsync(string slotId, CancellationToken ct)
            => Task.FromResult<BookingSlot?>(BookingSlot.Rehydrate(
                id: slotId,
                transactionRef: "tx-1",
                adviserId: "adv-1",
                adviserName: "Adviser One",
                startUtc: new DateTime(2026, 01, 02, 03, 04, 05, DateTimeKind.Utc),
                endUtc: new DateTime(2026, 01, 02, 04, 04, 05, DateTimeKind.Utc),
                score: 3,
                scoreBreakdown: null,
                locationRef: null,
                travelMinutes: null,
                companyBufferMinutes: null,
                distanceMiles: null,
                travelStatus: null,
                travelMessage: null,
                createdUtc: DateTime.UtcNow));
    }

    private sealed class StubTxRepo : IBookingTransactionRepository
    {
        public Task AddAsync(BookingTransaction transaction, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(BookingTransaction transaction, CancellationToken ct) => throw new NotSupportedException();
        public Task<BookingTransaction?> GetWithSlotsAsync(string transactionId, CancellationToken ct) => throw new NotSupportedException();
        public Task<BookingTransaction?> GetForUpdateAsync(string transactionId, CancellationToken ct) => throw new NotSupportedException();

        public Task<BookingTransaction?> GetAsync(string transactionId, CancellationToken ct)
            => Task.FromResult<BookingTransaction?>(BookingTransaction.Rehydrate(
                id: transactionId,
                transactionRef: "tx-1",
                proposedStartUtc: new DateTime(2026, 01, 02, 03, 04, 05, DateTimeKind.Utc),
                duration: TimeSpan.FromHours(1),
                timezone: "UTC",
                isRemote: true,
                meetingType: "Review",
                locationRef: null,
                status: BookingTransactionStatus.Open,
                createdUtc: DateTime.UtcNow,
                expiresUtc: null,
                slots: null));
    }

    private sealed class StubClientDirectory : IClientDirectory
    {
        public Task<ClientDirectoryItem?> GetAsync(string transactionIdOrClientId, CancellationToken ct)
            => Task.FromResult<ClientDirectoryItem?>(new ClientDirectoryItem
            {
                TransactionId = transactionIdOrClientId,
                PartnerLeadId = "lead-1",
                Email = "client@example.com",
                FirstName = "Client",
                LastName = "Example"
            });
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handle;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handle)
        {
            _handle = handle;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handle(request));
    }
}
