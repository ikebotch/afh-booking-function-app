using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Function.Http;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Function.Functions.V1.Config;

[BookingOpenApiTag("Config")]
public sealed class GetMeetingTopicsFunction
{
    private static readonly char[] TopicWhitespaceSeparators = [' ', '\t', '\r', '\n'];

    private readonly IOptions<BookingConfigOptions> _options;
    private readonly IAdviserProfileProjectionRepository _profiles;
    private readonly ILogger<GetMeetingTopicsFunction> _logger;

    public GetMeetingTopicsFunction(
        IOptions<BookingConfigOptions> options,
        IAdviserProfileProjectionRepository profiles,
        ILogger<GetMeetingTopicsFunction> logger)
    {
        _options = options;
        _profiles = profiles;
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
        var configuredTopics = _options.Value.MeetingTopics
            .Where(x => !string.IsNullOrWhiteSpace(x.Code))
            .Select(x => new MeetingTopicDto
            {
                Code = x.Code.Trim(),
                Label = string.IsNullOrWhiteSpace(x.Label) ? x.Code.Trim() : x.Label.Trim(),
                IsDefault = x.IsDefault,
                Source = "Configuration"
            })
            .GroupBy(x => NormalizeTopic(x.Code), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<AdviserProfileProjectionRecord> activeProfiles = [];
        var source = "ConfigurationAndAdviserSkills";

        try
        {
            activeProfiles = await _profiles.ListActiveAsync(ct);
        }
        catch (Exception ex)
        {
            source = "Configuration";
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
                        Source = "ConfigurationAndAdviserSkills"
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
            .Select(x => x.OrderByDescending(y => y.Source.Contains("Configuration", StringComparison.OrdinalIgnoreCase)).First())
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
