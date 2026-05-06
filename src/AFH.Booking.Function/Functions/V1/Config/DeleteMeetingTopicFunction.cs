using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Function.Http;

namespace AFH.Booking.Function.Functions.V1.Config;

[BookingOpenApiTag("Config")]
public sealed class DeleteMeetingTopicFunction
{
    private readonly IMeetingTopicRepository _topics;
    private readonly IUnitOfWork _uow;

    public DeleteMeetingTopicFunction(
        IMeetingTopicRepository topics,
        IUnitOfWork uow)
    {
        _topics = topics;
        _uow = uow;
    }

    [Function("Config_DeleteMeetingTopic")]
    [BookingOpenApiOperation("Config", "Delete meeting topic")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "v1/config/meeting-topics/{code}")]
        HttpRequestData req,
        string code,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "code is required.", ct, "Validation");

        var deleted = await _topics.DeactivateAsync(code, DateTime.UtcNow, ct);
        if (!deleted)
            return await req.ProblemAsync(HttpStatusCode.NotFound, $"Meeting topic '{code}' was not found.", ct, "NotFound");

        await _uow.SaveChangesAsync(ct);
        return await req.OkJsonAsync(new { code = code.Trim(), deleted = true }, ct);
    }
}
