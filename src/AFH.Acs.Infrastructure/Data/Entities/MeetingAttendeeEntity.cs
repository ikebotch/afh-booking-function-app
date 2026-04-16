namespace AFH.Acs.Recorder.Infrastructure.Data.Entities;

public class MeetingAttendeeEntity
{
    public string MeetingId { get; set; } = default!;
    public string Email { get; set; } = default!;

    public string Role { get; set; } = default!;           // ADVISER / CLIENT / OTHER
    public string ResponseStatus { get; set; } = "NONE";   // ACCEPTED / DECLINED / TENTATIVE / NONE
    public DateTime? ResponseTimeUtc { get; set; }

    public MeetingEntity Meeting { get; set; } = default!;
}