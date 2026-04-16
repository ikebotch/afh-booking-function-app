namespace AFH.Acs.Infrastructure.Persistence.Entities;

public sealed class MeetingAttendeeEntity
{
    public string MeetingId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string ResponseStatus { get; set; } = "None";
    public DateTime? ResponseTimeUtc { get; set; }
    public MeetingEntity Meeting { get; set; } = default!;
}
