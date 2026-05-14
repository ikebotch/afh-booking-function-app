using System.Net;

namespace AFH.Booking.Infrastructure.Clients;

public static class DownstreamFailureClassifier
{
    public static string Classify(HttpStatusCode? statusCode)
        => statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "AuthOrConfiguration",
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => "InvalidRequest",
            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => "Timeout",
            HttpStatusCode.NotFound => "NotFound",
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable => "Unavailable",
            _ when statusCode.HasValue && (int)statusCode.Value >= 500 => "InternalFailure",
            _ => "Unavailable"
        };
}
