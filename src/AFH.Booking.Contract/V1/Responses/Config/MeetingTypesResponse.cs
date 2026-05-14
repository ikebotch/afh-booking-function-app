namespace AFH.Booking.Contracts.V1.Responses;

public sealed class MeetingTypesResponse
{
    public string Source { get; init; } = "Configuration";
    public IReadOnlyList<MeetingTypeDto> MeetingTypes { get; init; } = [];
}

public sealed class MeetingTypeDto
{
    public string Code { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public int? DefaultDurationMinutes { get; init; }
}