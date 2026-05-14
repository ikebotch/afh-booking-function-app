using AFH.Booking.Function.Http;

namespace AFH.Booking.Function.Functions.V1;

[BookingOpenApiTag("Health")]
public sealed class HealthFunctionV1
{
    [Function("CalendarHealthV1")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/calendar/health")] HttpRequestData req,
        CancellationToken ct)
    {
        return await req.OkJsonAsync(new
        {
            status = "Healthy",
            timestampUtc = DateTime.UtcNow
        }, ct);
    }
}
