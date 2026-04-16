namespace AFH.Acs.Recorder.DTOs;

public class MeetingAttendeeDto
{
    public string Email { get; set; } = default!;
    public string Role { get; set; } = default!;            // Adviser, Client, etc.
    public string ResponseStatus { get; set; } = "None";    // Accepted, Declined, None
    public DateTimeOffset? ResponseTimeUtc { get; set; }
}