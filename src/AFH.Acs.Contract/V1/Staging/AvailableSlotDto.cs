namespace AFH.Acs.Recorder.DTOs;

public class AvailableSlotDto
{
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }

    /// <summary>
    /// Meeting types that can be used in this slot 
    /// (optional for future smart routing).
    /// </summary>
    public List<string> MeetingTypes { get; set; } = new();
}