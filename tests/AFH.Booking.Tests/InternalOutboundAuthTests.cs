using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Location.Travel;
using AFH.Booking.Domain.Options;
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
        var handler = new StubHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"joinUrl\":\"https://acs.example/meeting/abc\"}", Encoding.UTF8, "application/json")
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
            NullLogger<AcsMeetingLinkFactory>.Instance);

        var result = await sut.CreateJoinLinkAsync("booking-123", CancellationToken.None);

        Assert.Equal("https://acs.example/meeting/abc", result);
        Assert.NotNull(captured);
        Assert.True(captured!.Headers.TryGetValues("x-functions-key", out var functionKeyValues));
        Assert.Equal("acs-function-key", functionKeyValues!.Single());
        Assert.Equal("Bearer", captured.Headers.Authorization?.Scheme);
        Assert.Equal("acs-internal-token", captured.Headers.Authorization?.Parameter);
        Assert.Equal("/api/v1/meetings/link", captured.RequestUri!.AbsolutePath);
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
