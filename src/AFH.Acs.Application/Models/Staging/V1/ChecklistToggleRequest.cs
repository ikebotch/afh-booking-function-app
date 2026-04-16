namespace AFH.Acs.Recorder.Models.V1;

public class ChecklistToggleRequest
{
    /// <summary>
    /// True if the item should be marked complete, false if undone.
    /// </summary>
    public bool IsCompleted { get; set; }
}