namespace AFH.Acs.Recorder.DTOs;
public class AdviserAvailabilityDto
{
    /// <summary>
    /// Date range returned from Graph.
    /// </summary>
    public DateTimeOffset RangeStart { get; set; }
    public DateTimeOffset RangeEnd { get; set; }

    /// <summary>
    /// Raw free/busy blocks pulled from Graph API.
    /// </summary>
    public List<BusyBlockDto> Busy { get; set; } = new();

    /// <summary>
    /// Optional: Suggested free slots pre-computed by backend.
    /// </summary>
    public List<AvailableSlotDto> SuggestedSlots { get; set; } = new();
}