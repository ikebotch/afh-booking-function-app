namespace AFH.Acs.Contract.V1.Responses;

public sealed class MeetingLinkResponse
{
    public string BookingId { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;
    public string JoinCode { get; init; } = string.Empty;
    public string JoinUrl { get; init; } = string.Empty;
}
