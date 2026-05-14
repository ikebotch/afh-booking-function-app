using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Function.Http;
namespace AFH.Booking.Function.Functions.V1.Config;

[BookingOpenApiTag("Config")]
public sealed class GetMeetingTopicsFunction
{
    private static readonly char[] TopicWhitespaceSeparators = [' ', '\t', '\r', '\n'];

    private readonly IAdviserProfileProjectionRepository _profiles;
    private readonly IMeetingTopicRepository _topics;
    private readonly ILogger<GetMeetingTopicsFunction> _logger;

    public GetMeetingTopicsFunction(
        IAdviserProfileProjectionRepository profiles,
        IMeetingTopicRepository topics,
        ILogger<GetMeetingTopicsFunction> logger)
    {
        _profiles = profiles;
        _topics = topics;
        _logger = logger;
    }

    [Function("Config_GetMeetingTopics")]
    [BookingOpenApiOperation(
        "Config",
        "Get meeting topics",
        ResponseType = typeof(MeetingTopicsResponse))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/config/meeting-topics")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var configuredTopics = (await _topics.ListActiveAsync(ct))
            .Where(x => !string.IsNullOrWhiteSpace(x.Code))
            .Select(x => new MeetingTopicDto
            {
                Code = x.Code.Trim(),
                Label = string.IsNullOrWhiteSpace(x.Label) ? x.Code.Trim() : x.Label.Trim(),
                IsDefault = x.IsDefault,
                Source = "MeetingTopics"
            })
            .GroupBy(x => NormalizeTopic(x.Code), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<AdviserProfileProjectionRecord> activeProfiles = [];
        var source = "MeetingTopicsAndAdviserSkills";

        try
        {
            activeProfiles = await _profiles.ListActiveAsync(ct);
        }
        catch (Exception ex)
        {
            source = "MeetingTopics";
            _logger.LogWarning(ex, "Unable to load adviser skills for meeting topics. Returning configured topics only.");
        }

        var adviserSkillTopics = activeProfiles
            .SelectMany(x => x.Skills ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x =>
            {
                var code = x.Trim();
                var normalized = NormalizeTopic(code);

                return configuredTopics.TryGetValue(normalized, out var configured)
                    ? new MeetingTopicDto
                    {
                        Code = configured.Code,
                        Label = configured.Label,
                        IsDefault = configured.IsDefault,
                        Source = "MeetingTopicsAndAdviserSkills"
                    }
                    : new MeetingTopicDto
                    {
                        Code = code,
                        Label = code,
                        Source = "AdviserSkills"
                    };
            });

        var meetingTopics = configuredTopics.Values
            .Concat(adviserSkillTopics)
            .GroupBy(x => NormalizeTopic(x.Code), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.OrderByDescending(y => y.Source.Contains("MeetingTopics", StringComparison.OrdinalIgnoreCase)).First())
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return await req.OkJsonAsync(new MeetingTopicsResponse
        {
            Source = source,
            MeetingTopics = meetingTopics
        }, ct);
    }

    private static string NormalizeTopic(string value)
    {
        return string.Join(" ", value
            .Trim()
            .Split(TopicWhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}