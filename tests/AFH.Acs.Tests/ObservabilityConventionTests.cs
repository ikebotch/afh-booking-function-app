using AFH.Acs.Function.Middleware;

namespace AFH.Acs.Tests;

public sealed class ObservabilityConventionTests
{
    [Fact]
    public void CorrelationIdMiddleware_UsesCanonicalHeaderAndItemKey()
    {
        Assert.Equal("x-correlation-id", CorrelationIdMiddleware.HeaderName);
        Assert.Equal("correlation-id", CorrelationIdMiddleware.ItemKey);
    }
}
