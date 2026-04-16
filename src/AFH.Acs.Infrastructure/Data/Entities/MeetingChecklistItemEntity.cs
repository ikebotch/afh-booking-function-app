namespace AFH.Acs.Recorder.Infrastructure.Data.Entities;

public class MeetingChecklistItemEntity
{
    public string MeetingId { get; set; } = default!;
    public string ItemId { get; set; } = default!;

    public string DisplayText { get; set; } = default!;
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public MeetingEntity Meeting { get; set; } = default!;
}