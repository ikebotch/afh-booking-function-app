using AFH.Booking.Application.Bookings;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Tests;

public sealed class DownstreamPerformedByResolverTests
{
    [Fact]
    public void Resolve_SelfServiceClient_ReturnsSelf()
    {
        var actor = BookingActorContext.SelfServiceClient("client-1");

        Assert.Equal("Self", DownstreamPerformedByResolver.Resolve(actor));
    }

    [Fact]
    public void Resolve_NamedPartner_IncludesPartnerName()
    {
        var actor = BookingActorContext.Partner("LeadTech");

        Assert.Equal("Partner:LeadTech", DownstreamPerformedByResolver.Resolve(actor));
    }

    [Theory]
    [InlineData(BookingActorContext.ActorInternalAdmin)]
    [InlineData(BookingActorContext.ActorAdviser)]
    [InlineData(BookingActorContext.ActorManager)]
    [InlineData(BookingActorContext.ActorSystem)]
    public void Resolve_AfhActor_ReturnsAfh(string actorType)
    {
        var actor = BookingActorContext.InternalAdmin(actorType: actorType);

        Assert.Equal("AFH", DownstreamPerformedByResolver.Resolve(actor));
    }

    [Fact]
    public void Resolve_UnknownFutureActor_PreservesActorType()
    {
        Assert.Equal("Introducer", DownstreamPerformedByResolver.Resolve(null, "Introducer"));
    }
}
