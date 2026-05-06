namespace AFH.Booking.Contracts.V1.Responses;

public sealed class MeetingTopicsResponse
{
    public string Source { get; init; } = "MeetingTopicsAndAdviserSkills";
    public IReadOnlyList<MeetingTopicDto> MeetingTopics { get; init; } = [];
}

public sealed class MeetingTopicDto
{
    public string Code { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public string Source { get; init; } = string.Empty;
}
