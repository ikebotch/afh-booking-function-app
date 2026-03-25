using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Location.Travel;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Auth;
using AFH.Booking.Infrastructure.Calendar;
using AFH.Booking.Infrastructure.Location;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace AFH.Booking.Tests;

public class InternalOutboundAuthTests
{
    [Fact]
    public async Task CalendarGateway_UsesBearerAuth_WithoutCodeQuery()
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
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("calendar-token", captured.Headers.Authorization?.Parameter);
        Assert.DoesNotContain("code=", captured.RequestUri!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TravelMatrixService_UsesBearerAuth_WithoutCodeQuery()
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
                InternalToken = "location-token"
            }),
            new InternalBearerServiceAuthenticator(),
            NullLogger<TravelMatrixService>.Instance);

        await sut.GetAsync(new TravelMatrixRequest
        {
            RequestId = "req-1"
        }, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("location-token", captured.Headers.Authorization?.Parameter);
        Assert.DoesNotContain("code=", captured.RequestUri!.ToString(), StringComparison.OrdinalIgnoreCase);
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
