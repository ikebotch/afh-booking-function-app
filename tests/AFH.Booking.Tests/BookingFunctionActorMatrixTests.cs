using System.Net;
using System.Text;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Functions.V1.Bookings;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFH.Booking.Tests;

public sealed class BookingFunctionActorMatrixTests
{
    [Fact]
    public async Task LeadTechCancel_MapsLeadTechActorAndUsesSharedCancelService()
    {
        var service = new CapturingCancelBookingService();
        var sut = new LeadTechCancelBookingFunction(service);
        var request = CreateJsonRequest("""{"reasonCode":"LEADTECH_REQUEST","reasonDetail":"Client called LeadTech"}""");
        request.Headers.Add("x-correlation-id", "corr-leadtech");

        var response = await sut.Run(request, " booking-1 ", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("booking-1", service.LastCommand?.BookingId);
        Assert.Equal(LifecycleActors.LeadTech, service.LastCommand?.RequestedBy);
        Assert.Equal(BookingActorContext.SourceLeadTech, service.LastCommand?.ActorContext?.SourceApplication);
        Assert.Equal(LifecycleActors.LeadTech, service.LastCommand?.ActorContext?.ActorType);
        Assert.Equal("corr-leadtech", service.LastCommand?.CorrelationId);
        Assert.Equal("LEADTECH_REQUEST", service.LastCommand?.ReasonCode);
    }

    [Fact]
    public async Task InternalCancel_DefaultsToInternalAdminActorAndUsesSharedCancelService()
    {
        var service = new CapturingCancelBookingService();
        var sut = new CancelBookingFunction(service, NullLogger<CancelBookingFunction>.Instance);
        var request = CreateJsonRequest("""{"reasonCode":"ADMIN_REQUEST","reasonDetail":"Back office change"}""");
        request.Headers.Add("x-correlation-id", "corr-admin");

        var response = await sut.Run(request, " booking-1 ", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("booking-1", service.LastCommand?.BookingId);
        Assert.Equal(BookingActorContext.ActorInternalAdmin, service.LastCommand?.RequestedBy);
        Assert.NotEqual(LifecycleActors.Client, service.LastCommand?.RequestedBy);
        Assert.Equal(BookingActorContext.SourceInternalAdmin, service.LastCommand?.ActorContext?.SourceApplication);
        Assert.Equal(BookingActorContext.ActorInternalAdmin, service.LastCommand?.ActorContext?.ActorType);
        Assert.Equal("corr-admin", service.LastCommand?.CorrelationId);
    }

    [Fact]
    public async Task LeadTechRearrange_MapsLeadTechActorAndUsesSharedRearrangeService()
    {
        var service = new CapturingRearrangeBookingService();
        var sut = new LeadTechRearrangeBookingFunction(service);
        var request = CreateJsonRequest("""{"newSlotId":"slot-new","reasonCode":"LEADTECH_RESCHEDULE"}""");
        request.Headers.Add("x-correlation-id", "corr-leadtech");

        var response = await sut.Run(request, " booking-old ", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("booking-old", service.LastCommand?.BookingId);
        Assert.Equal("slot-new", service.LastCommand?.NewSlotId);
        Assert.Equal(LifecycleActors.LeadTech, service.LastCommand?.RequestedBy);
        Assert.Equal(BookingActorContext.SourceLeadTech, service.LastCommand?.ActorContext?.SourceApplication);
        Assert.Equal(LifecycleActors.LeadTech, service.LastCommand?.ActorContext?.ActorType);
        Assert.Equal("corr-leadtech", service.LastCommand?.CorrelationId);
    }

    [Fact]
    public async Task InternalRearrange_DefaultsToInternalAdminActorAndUsesSharedRearrangeService()
    {
        var service = new CapturingRearrangeBookingService();
        var sut = new RearrangeBookingFunction(service);
        var request = CreateJsonRequest("""{"newSlotId":"slot-new","reasonCode":"ADMIN_RESCHEDULE"}""");
        request.Headers.Add("x-correlation-id", "corr-admin");

        var response = await sut.Run(request, " booking-old ", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("booking-old", service.LastCommand?.BookingId);
        Assert.Equal("slot-new", service.LastCommand?.NewSlotId);
        Assert.Equal(BookingActorContext.ActorInternalAdmin, service.LastCommand?.RequestedBy);
        Assert.NotEqual(LifecycleActors.Client, service.LastCommand?.RequestedBy);
        Assert.Equal(BookingActorContext.SourceInternalAdmin, service.LastCommand?.ActorContext?.SourceApplication);
        Assert.Equal(BookingActorContext.ActorInternalAdmin, service.LastCommand?.ActorContext?.ActorType);
        Assert.Equal("corr-admin", service.LastCommand?.CorrelationId);
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

    private sealed class CapturingCancelBookingService : ICancelBookingService
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

    private sealed class CapturingRearrangeBookingService : IRearrangeBookingService
    {
        public RearrangeBookingCommand? LastCommand { get; private set; }

        public Task<Result<RearrangeBookingResponse>> HandleAsync(RearrangeBookingCommand cmd, CancellationToken ct)
        {
            LastCommand = cmd;
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
