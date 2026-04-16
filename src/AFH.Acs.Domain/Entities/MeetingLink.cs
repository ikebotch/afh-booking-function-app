namespace AFH.Acs.Domain.Entities;

public sealed class MeetingLink
{
    public string BookingId { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;
    public string JoinCode { get; init; } = string.Empty;
    public string JoinUrl { get; init; } = string.Empty;
}
