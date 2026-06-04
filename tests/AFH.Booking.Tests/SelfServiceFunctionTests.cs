using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Models.Availability;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Functions.V1.Bookings;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace AFH.Booking.Tests;

public sealed class SelfServiceFunctionTests
{
    [Fact]
    public async Task ViewBooking_ValidQueryToken_ValidatesTokenAndReturnsDetails()
    {
        var access = new StubAccessService();
        var details = new StubBookingDetailsService();
        var sut = new SelfServiceGetBookingDetailsFunction(access, details);
        var request = TestHttpRequestData.Create(
            new Uri("https://localhost/api/v1/self-service/bookings/booking-1?token=client-token"));

        var response = await sut.Run(request, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("booking-1", access.LastBookingId);
        Assert.Equal("client-token", access.LastToken);
        Assert.Equal("booking-1", details.LastQuery?.BookingId);
    }

    [Fact]
    public async Task ViewBooking_GeneratedLinkEncodedToken_ValidatesDecodedToken()
    {
        var access = new StubAccessService();
        var details = new StubBookingDetailsService();
        var sut = new SelfServiceGetBookingDetailsFunction(access, details);
        var request = TestHttpRequestData.Create(
            new Uri("https://localhost/api/v1/self-service/bookings/booking-1?token=opaque%2B%2F%3D%20token"));

        var response = await sut.Run(request, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("opaque+/= token", access.LastToken);
    }

    [Fact]
    public async Task ViewBooking_InvalidToken_ReturnsUnauthorizedAndDoesNotCallDetails()
    {
        var access = new StubAccessService(Result<BookingChangeActorContext>.Fail(
            HttpStatusCode.Unauthorized,
            "Client token format is invalid.",
            Errors.Unauthorized));
        var details = new StubBookingDetailsService();
        var sut = new SelfServiceGetBookingDetailsFunction(access, details);
        var request = TestHttpRequestData.Create(
            new Uri("https://localhost/api/v1/self-service/bookings/booking-1?token=bad-token"));

        var response = await sut.Run(request, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(details.LastQuery);
    }

    [Fact]
    public async Task ViewBooking_WrongBookingToken_ReturnsForbiddenAndDoesNotCallDetails()
    {
        var access = new StubAccessService(Result<BookingChangeActorContext>.Fail(
            HttpStatusCode.Forbidden,
            "Client token does not match booking.",
            Errors.Unauthorized));
        var details = new StubBookingDetailsService();
        var sut = new SelfServiceGetBookingDetailsFunction(access, details);
        var request = TestHttpRequestData.Create(
            new Uri("https://localhost/api/v1/self-service/bookings/booking-2?token=booking-1-token"));

        var response = await sut.Run(request, "booking-2", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(details.LastQuery);
    }

    [Fact]
    public async Task ViewBooking_ExpiredToken_ReturnsUnauthorizedAndDoesNotCallDetails()
    {
        var access = new StubAccessService(Result<BookingChangeActorContext>.Fail(
            HttpStatusCode.Unauthorized,
            "Client token has expired.",
            Errors.Unauthorized));
        var details = new StubBookingDetailsService();
        var sut = new SelfServiceGetBookingDetailsFunction(access, details);
        var request = TestHttpRequestData.Create(
            new Uri("https://localhost/api/v1/self-service/bookings/booking-1?token=expired-token"));

        var response = await sut.Run(request, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(details.LastQuery);
    }

    [Fact]
    public async Task CancelBooking_ValidHeaderToken_UsesClientActor()
    {
        var access = new StubAccessService();
        var service = new StubCancelBookingService();
        var sut = new SelfServiceCancelBookingFunction(access, service);
        var request = CreateJsonRequest("""{"reasonCode":"CLIENT_REQUEST","reason":"No longer needed"}""");
        request.Headers.Add("x-booking-access-token", "client-token");

        var response = await sut.Run(request, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("booking-1", access.LastBookingId);
        Assert.Equal("client-token", access.LastToken);
        Assert.Equal("booking-1", service.LastCommand?.BookingId);
        Assert.Equal(LifecycleActors.Client, service.LastCommand?.RequestedBy);
        Assert.Equal("client-actor", service.LastCommand?.ActorId);
        Assert.Equal(BookingActorContext.SourceSelfService, service.LastCommand?.ActorContext?.SourceApplication);
        Assert.Equal(LifecycleActors.Client, service.LastCommand?.ActorContext?.ActorType);
        Assert.True(service.LastCommand?.ActorContext?.IsSelfService);
    }

    [Fact]
    public async Task RearrangementOptions_ValidToken_CallsApplicationService()
    {
        var access = new StubAccessService();
        var service = new StubRearrangementOptionsService();
        var sut = new SelfServiceRearrangementOptionsFunction(access, service);
        var request = CreateJsonRequest("""{"duration":45,"limit":5}""");
        request.Headers.Add("x-booking-access-token", "client-token");

        var response = await sut.Run(request, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("booking-1", access.LastBookingId);
        Assert.Equal("booking-1", service.LastCommand?.BookingId);
        Assert.Equal(45, service.LastCommand?.Duration);
        Assert.Equal(5, service.LastCommand?.Limit);
        Assert.Equal(BookingActorContext.SourceSelfService, service.LastCommand?.ActorContext?.SourceApplication);
        Assert.Equal(LifecycleActors.Client, service.LastCommand?.ActorContext?.ActorType);
        Assert.True(service.LastCommand?.ActorContext?.IsSelfService);
    }

    [Fact]
    public async Task RearrangementOptions_ResponseIncludesTopLevelTransactionId()
    {
        var access = new StubAccessService();
        var service = new StubRearrangementOptionsService();
        var sut = new SelfServiceRearrangementOptionsFunction(access, service);
        var request = CreateJsonRequest("""{"duration":45,"limit":5}""");
        request.Headers.Add("x-booking-access-token", "client-token");

        var response = await sut.Run(request, "booking-1", CancellationToken.None);

        var json = await ReadJsonAsync(response);
        var data = GetData(json);
        Assert.Equal("tx-1", GetString(data, "transactionId"));
        Assert.Equal("tx-1", GetString(GetObject(data, "assignedAdviserOptions"), "transactionId"));
    }

    [Fact]
    public async Task RearrangeBooking_ValidToken_UsesClientActor()
    {
        var access = new StubAccessService();
        var service = new StubRearrangeBookingService();
        var sut = new SelfServiceRearrangeBookingFunction(access, service);
        var request = CreateJsonRequest("""{"newSlotId":"slot-new","reasonCode":"CLIENT_RESCHEDULE"}""");
        request.Headers.Add("x-booking-access-token", "client-token");

        var response = await sut.Run(request, "booking-old", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("booking-old", access.LastBookingId);
        Assert.Equal("booking-old", service.LastCommand?.BookingId);
        Assert.Equal("slot-new", service.LastCommand?.NewSlotId);
        Assert.Equal(LifecycleActors.Client, service.LastCommand?.RequestedBy);
        Assert.Equal("client-actor", service.LastCommand?.ActorId);
        Assert.Equal(BookingActorContext.SourceSelfService, service.LastCommand?.ActorContext?.SourceApplication);
        Assert.Equal(LifecycleActors.Client, service.LastCommand?.ActorContext?.ActorType);
        Assert.True(service.LastCommand?.ActorContext?.IsSelfService);
    }

    [Fact]
    public async Task RearrangeBooking_UsesCurrentRouteBookingIdAndDoesNotReadNewBookingIdFromCaller()
    {
        var access = new StubAccessService();
        var service = new StubRearrangeBookingService();
        var sut = new SelfServiceRearrangeBookingFunction(access, service);
        var request = CreateJsonRequest("""{"newBookingId":"caller-owned-id","newSlotId":"slot-new","reasonCode":"CLIENT_RESCHEDULE"}""");
        request.Headers.Add("x-booking-access-token", "client-token");

        var response = await sut.Run(request, "current-booking", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("current-booking", access.LastBookingId);
        Assert.Equal("current-booking", service.LastCommand?.BookingId);
        Assert.Equal("slot-new", service.LastCommand?.NewSlotId);
    }

    [Fact]
    public async Task RearrangeBooking_TokenBookingMismatch_ReturnsForbiddenAndDoesNotCallService()
    {
        var access = new StubAccessService(Result<BookingChangeActorContext>.Fail(
            HttpStatusCode.Forbidden,
            "Client token does not match booking.",
            Errors.Unauthorized));
        var service = new StubRearrangeBookingService();
        var sut = new SelfServiceRearrangeBookingFunction(access, service);
        var request = CreateJsonRequest("""{"newSlotId":"slot-new","reasonCode":"CLIENT_RESCHEDULE"}""");
        request.Headers.Add("x-booking-access-token", "booking-1-token");

        var response = await sut.Run(request, "booking-2", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(service.LastCommand);
    }

    [Fact]
    public async Task RearrangeBooking_MissingNewSlotId_ReturnsBadRequestAndDoesNotCallService()
    {
        var access = new StubAccessService();
        var service = new StubRearrangeBookingService();
        var sut = new SelfServiceRearrangeBookingFunction(access, service);
        var request = CreateJsonRequest("""{"reasonCode":"CLIENT_RESCHEDULE"}""");
        request.Headers.Add("x-booking-access-token", "client-token");

        var response = await sut.Run(request, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(service.LastCommand);
    }

    [Fact]
    public async Task RearrangeBooking_UnavailableSelectedSlot_ReturnsConflict()
    {
        var access = new StubAccessService();
        var service = new StubRearrangeBookingService(Result<RearrangeBookingResponse>.Fail(
            HttpStatusCode.Conflict,
            "The selected slot is no longer available.",
            Errors.SlotNoLongerAvailable));
        var sut = new SelfServiceRearrangeBookingFunction(access, service);
        var request = CreateJsonRequest("""{"newSlotId":"slot-new","reasonCode":"CLIENT_RESCHEDULE"}""");
        request.Headers.Add("x-booking-access-token", "client-token");

        var response = await sut.Run(request, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal(Errors.SlotNoLongerAvailable, GetData(json)["code"]!.GetValue<string>());
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

    private static async Task<JsonObject> ReadJsonAsync(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body);
        return JsonNode.Parse(await reader.ReadToEndAsync())!.AsObject();
    }

    private static JsonObject GetData(JsonObject envelope) =>
        (envelope["data"] ?? envelope["Data"])!.AsObject();

    private static JsonObject GetObject(JsonObject source, string camelName)
    {
        var pascalName = char.ToUpperInvariant(camelName[0]) + camelName[1..];
        return (source[camelName] ?? source[pascalName])!.AsObject();
    }

    private static string GetString(JsonObject source, string camelName)
    {
        var pascalName = char.ToUpperInvariant(camelName[0]) + camelName[1..];
        return (source[camelName] ?? source[pascalName])!.GetValue<string>();
    }

    private sealed class StubAccessService(Result<BookingChangeActorContext>? result = null) : IBookingChangeAccessService
    {
        private readonly Result<BookingChangeActorContext> _result = result
            ?? Result<BookingChangeActorContext>.Ok(new BookingChangeActorContext(
                LifecycleActors.Client,
                "client-actor",
                "TRX-1",
                "token-correlation"));

        public string? LastBookingId { get; private set; }
        public string? LastToken { get; private set; }

        public Task<Result<BookingChangeActorContext>> ValidateClientTokenAsync(string bookingId, string? token, CancellationToken ct)
        {
            LastBookingId = bookingId;
            LastToken = token;
            return Task.FromResult(_result);
        }

        public Task<Result<string>> GenerateClientTokenAsync(string bookingId, CancellationToken ct)
            => Task.FromResult(Result<string>.Ok("new-token"));
    }

    private sealed class StubBookingDetailsService : IBookingDetailsService
    {
        public GetBookingDetailsQuery? LastQuery { get; private set; }

        public Task<Result<BookingDetailsResponse>> HandleAsync(GetBookingDetailsQuery query, CancellationToken ct)
        {
            LastQuery = query;
            return Task.FromResult(Result<BookingDetailsResponse>.Ok(new BookingDetailsResponse
            {
                BookingId = query.BookingId,
                SlotId = "slot-1",
                TransactionId = "tx-1",
                TransactionRef = "TRX-1",
                AdviserId = "adv-1",
                AdviserName = "Adviser One",
                StartUtc = new DateTime(2026, 06, 01, 9, 0, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 06, 01, 10, 0, 0, DateTimeKind.Utc),
                DurationMinutes = 60,
                Status = "Confirmed"
            }));
        }
    }

    private sealed class StubCancelBookingService : ICancelBookingService
    {
        public CancelBookingCommand? LastCommand { get; private set; }

        public Task<Result<CancelBookingResponse>> HandleAsync(CancelBookingCommand cmd, CancellationToken ct)
        {
            LastCommand = cmd;
            return Task.FromResult(Result<CancelBookingResponse>.Ok(new CancelBookingResponse
            {
                BookingId = cmd.BookingId,
                CancelledUtc = new DateTime(2026, 06, 01, 8, 0, 0, DateTimeKind.Utc),
                Status = "Cancelled"
            }));
        }
    }

    private sealed class StubRearrangementOptionsService : IRearrangementOptionsService
    {
        public GetRearrangementOptionsCommand? LastCommand { get; private set; }

        public Task<Result<RearrangementOptionsResponse>> HandleAsync(GetRearrangementOptionsCommand cmd, CancellationToken ct)
        {
            LastCommand = cmd;
            return Task.FromResult(Result<RearrangementOptionsResponse>.Ok(new RearrangementOptionsResponse
            {
                BookingId = cmd.BookingId,
                TransactionId = "tx-1",
                AssignedAdviserId = "adv-1",
                AssignedAdviserName = "Adviser One",
                AssignedAdviserOptions = new GetAvailabilityResponse { TransactionId = "tx-1" },
                AlternativeAdviserOptions = new GetAvailabilityResponse()
            }));
        }
    }

    private sealed class StubRearrangeBookingService(Result<RearrangeBookingResponse>? result = null) : IRearrangeBookingService
    {
        private readonly Result<RearrangeBookingResponse>? _result = result;
        public RearrangeBookingCommand? LastCommand { get; private set; }

        public Task<Result<RearrangeBookingResponse>> HandleAsync(RearrangeBookingCommand cmd, CancellationToken ct)
        {
            LastCommand = cmd;
            if (_result is not null)
                return Task.FromResult(_result);

            return Task.FromResult(Result<RearrangeBookingResponse>.Ok(new RearrangeBookingResponse
            {
                PreviousBookingId = cmd.BookingId,
                NewBookingId = "booking-new",
                NewSlotId = cmd.NewSlotId,
                PreviousAdviserId = "adv-old",
                PreviousAdviserName = "Old Adviser",
                PreviousStartUtc = new DateTime(2026, 06, 01, 9, 0, 0, DateTimeKind.Utc),
                PreviousEndUtc = new DateTime(2026, 06, 01, 10, 0, 0, DateTimeKind.Utc),
                NewAdviserId = "adv-new",
                NewAdviserName = "New Adviser",
                NewStartUtc = new DateTime(2026, 06, 02, 9, 0, 0, DateTimeKind.Utc),
                NewEndUtc = new DateTime(2026, 06, 02, 10, 0, 0, DateTimeKind.Utc),
                NotificationSummary = "Rescheduled"
            }));
        }
    }
}
