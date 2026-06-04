using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Functions.V1.Bookings;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Text;

namespace AFH.Booking.Tests;

public sealed class BookingActorContextTests
{
    [Fact]
    public void ActorContext_SelfServiceClient_MapsClientActor()
    {
        var context = BookingActorContext.SelfServiceClient("client-1", "corr-1");

        Assert.Equal(BookingActorContext.SourceSelfService, context.SourceApplication);
        Assert.Equal(LifecycleActors.Client, context.ActorType);
        Assert.Equal("client-1", context.ActorId);
        Assert.Equal("corr-1", context.CorrelationId);
        Assert.True(context.IsSelfService);
        Assert.False(context.CanOverrideRules);
    }

    [Fact]
    public void ActorContext_LeadTech_MapsLeadTechSource()
    {
        var context = BookingActorContext.LeadTech(
            actorId: "leadtech-user",
            displayName: "LeadTech User",
            correlationId: "corr-1",
            permissions: ["booking.cancel"]);

        Assert.Equal(BookingActorContext.SourceLeadTech, context.SourceApplication);
        Assert.Equal(LifecycleActors.LeadTech, context.ActorType);
        Assert.Equal("leadtech-user", context.ActorId);
        Assert.Contains("booking.cancel", context.Permissions);
        Assert.False(context.IsSelfService);
    }

    [Fact]
    public void ActorContext_InternalAdmin_DoesNotDefaultToClient()
    {
        var context = BookingActorContext.InternalAdmin(correlationId: "corr-1");

        Assert.Equal(BookingActorContext.SourceInternalAdmin, context.SourceApplication);
        Assert.Equal(BookingActorContext.ActorInternalAdmin, context.ActorType);
        Assert.NotEqual(LifecycleActors.Client, context.ActorType);
        Assert.False(context.IsSelfService);
    }

    [Fact]
    public void ActorContext_SystemJob_MapsSystemActor()
    {
        var context = BookingActorContext.SystemJob("HoldsCleanup", "corr-1");

        Assert.Equal(BookingActorContext.SourceSystemJob, context.SourceApplication);
        Assert.Equal(LifecycleActors.System, context.ActorType);
        Assert.Equal("HoldsCleanup", context.ActorId);
        Assert.True(context.CanOverrideRules);
    }

    [Fact]
    public void ActorContext_CommandCompatibility_DerivesLegacyFields()
    {
        var actor = BookingActorContext.LeadTech(
            actorId: "leadtech-user",
            correlationId: "corr-1");

        var cancel = new CancelBookingCommand
        {
            BookingId = "booking-1",
            ActorContext = actor,
            RequestedBy = "Client",
            ActorId = "legacy-user",
            CorrelationId = "legacy-corr"
        };

        var rearrange = new RearrangeBookingCommand
        {
            BookingId = "booking-1",
            NewSlotId = "slot-1",
            ActorContext = actor,
            RequestedBy = "Client",
            ActorId = "legacy-user",
            CorrelationId = "legacy-corr"
        };

        var noShow = new RecordNoShowCommand
        {
            BookingId = "booking-1",
            ActorContext = actor,
            RequestedBy = "Client",
            ActorId = "legacy-user",
            CorrelationId = "legacy-corr"
        };

        Assert.Equal(LifecycleActors.LeadTech, cancel.RequestedBy);
        Assert.Equal("leadtech-user", cancel.ActorId);
        Assert.Equal("corr-1", cancel.CorrelationId);
        Assert.Equal(LifecycleActors.LeadTech, rearrange.RequestedBy);
        Assert.Equal("leadtech-user", rearrange.ActorId);
        Assert.Equal("corr-1", rearrange.CorrelationId);
        Assert.Equal(LifecycleActors.LeadTech, noShow.RequestedBy);
        Assert.Equal("leadtech-user", noShow.ActorId);
        Assert.Equal("corr-1", noShow.CorrelationId);
    }

    [Fact]
    public async Task LeadTechCancelFunction_AddsLeadTechActorContext()
    {
        var service = new StubCancelBookingService();
        var sut = new LeadTechCancelBookingFunction(service);
        var request = CreateJsonRequest("""{"reasonCode":"LEADTECH_REQUEST"}""");
        request.Headers.Add("x-correlation-id", "corr-leadtech");

        var response = await sut.Run(request, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(BookingActorContext.SourceLeadTech, service.LastCommand?.ActorContext?.SourceApplication);
        Assert.Equal(LifecycleActors.LeadTech, service.LastCommand?.ActorContext?.ActorType);
        Assert.Equal("corr-leadtech", service.LastCommand?.CorrelationId);
    }

    [Fact]
    public async Task InternalCancelFunction_DefaultActorContext_IsNotClient()
    {
        var service = new StubCancelBookingService();
        var sut = new CancelBookingFunction(service, NullLogger<CancelBookingFunction>.Instance);
        var request = CreateJsonRequest("""{"reasonCode":"ADMIN_REQUEST"}""");

        var response = await sut.Run(request, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(BookingActorContext.SourceInternalAdmin, service.LastCommand?.ActorContext?.SourceApplication);
        Assert.Equal(BookingActorContext.ActorInternalAdmin, service.LastCommand?.ActorContext?.ActorType);
        Assert.NotEqual(LifecycleActors.Client, service.LastCommand?.RequestedBy);
    }

    [Fact]
    public async Task HoldsCleanup_UsesSystemActorContextForReleaseCommand()
    {
        var expiredHold = BookingHold.Rehydrate(
            "hold-1",
            "slot-1",
            "user-1",
            BookingHoldStatus.Active,
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddMinutes(-1),
            null,
            null,
            null,
            null,
            null,
            null);
        var holds = new Mock<IBookingHoldRepository>();
        holds.Setup(x => x.GetExpiredActiveAsync(It.IsAny<DateTime>(), 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync([expiredHold]);
        var release = new StubReleaseHoldService();
        var sut = new HoldsCleanupFunction(
            holds.Object,
            release,
            new StubClock(DateTime.UtcNow),
            NullLogger<HoldsCleanupFunction>.Instance);

        await sut.Run(null!, CancellationToken.None);

        Assert.Equal("hold-1", release.LastCommand?.HoldId);
        Assert.Equal(LifecycleActors.System, release.LastCommand?.ActorContext?.ActorType);
        Assert.Equal("HoldsCleanup", release.LastCommand?.ActorContext?.ActorId);
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

    private sealed class StubReleaseHoldService : IReleaseHoldService
    {
        public ReleaseHoldCommand? LastCommand { get; private set; }

        public Task<Result<ReleaseHoldResponse>> HandleAsync(string holdId, CancellationToken ct)
            => HandleAsync(new ReleaseHoldCommand { HoldId = holdId }, ct);

        public Task<Result<ReleaseHoldResponse>> HandleAsync(ReleaseHoldCommand command, CancellationToken ct)
        {
            LastCommand = command;
            return Task.FromResult(Result<ReleaseHoldResponse>.Ok(new ReleaseHoldResponse
            {
                BookingId = command.HoldId
            }));
        }
    }

    private sealed class StubClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
