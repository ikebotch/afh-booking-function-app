using AFH.Booking.Functions.Http;

namespace AFH.Calendar.Functions.Functions.V1;

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