using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Function.Http;

namespace AFH.Booking.Function.Functions.V1.Config;

[BookingOpenApiTag("Config")]
public sealed class UpsertMeetingTopicFunction
{
    private readonly IMeetingTopicRepository _topics;
    private readonly IUnitOfWork _uow;

    public UpsertMeetingTopicFunction(
        IMeetingTopicRepository topics,
        IUnitOfWork uow)
    {
        _topics = topics;
        _uow = uow;
    }

    [Function("Config_UpsertMeetingTopic")]
    [BookingOpenApiOperation(
        "Config",
        "Create or update meeting topic",
        RequestBodyType = typeof(UpsertMeetingTopicRequest),
        ResponseType = typeof(MeetingTopicDto))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "v1/config/meeting-topics/{code}")]
        HttpRequestData req,
        string code,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "code is required.", ct, "Validation");

        var body = await req.ReadJsonAsync<UpsertMeetingTopicRequest>(ct) ?? new UpsertMeetingTopicRequest();
        var topic = await _topics.UpsertAsync(new MeetingTopicUpsert
        {
            Code = code,
            Label = body.Label ?? code,
            IsDefault = body.IsDefault,
            IsActive = body.IsActive,
            SortOrder = body.SortOrder,
            ChangedUtc = DateTime.UtcNow
        }, ct);
        await _uow.SaveChangesAsync(ct);

        return await req.OkJsonAsync(new MeetingTopicDto
        {
            Code = topic.Code,
            Label = topic.Label,
            IsDefault = topic.IsDefault,
            Source = "MeetingTopics"
        }, ct);
    }
}
