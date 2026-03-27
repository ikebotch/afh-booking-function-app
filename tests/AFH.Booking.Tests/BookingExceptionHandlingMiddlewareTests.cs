using AFH.Booking.Functions.Middleware;
using System.Net;
using System.Text.Json;

namespace AFH.Booking.Tests;

public class BookingExceptionHandlingMiddlewareTests
{
    [Fact]
    public void MapException_MapsJsonExceptionToBadRequest()
    {
        var mapping = ExceptionHandlingMiddleware.MapException(new JsonException("Bad JSON"));

        Assert.NotNull(mapping);
        Assert.Equal(HttpStatusCode.BadRequest, mapping!.StatusCode);
        Assert.Equal("InvalidJson", mapping.FailureCode);
        Assert.Equal("RequestDeserialization", mapping.FailureSource);
    }

    [Fact]
    public void MapException_MapsDownstreamUnauthorizedToDependencyAuthFailed()
    {
        var ex = new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized);

        var mapping = ExceptionHandlingMiddleware.MapException(ex);

        Assert.NotNull(mapping);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, mapping!.StatusCode);
        Assert.Equal("DependencyAuthFailed", mapping.FailureCode);
        Assert.Equal("DownstreamDependency", mapping.FailureSource);
        Assert.Equal("AuthOrConfiguration", mapping.DownstreamCategory);
        Assert.Equal(401, mapping.DownstreamStatusCode);
    }
}
