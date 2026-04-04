using AFH.Booking.Function.Middleware;

namespace AFH.Booking.Tests;

public sealed class ObservabilityConventionTests
{
    [Fact]
    public void CorrelationIdMiddleware_UsesCanonicalHeaderAndItemKey()
    {
        Assert.Equal("x-correlation-id", CorrelationIdMiddleware.HeaderName);
        Assert.Equal("correlation-id", CorrelationIdMiddleware.ItemKey);
    }
}
