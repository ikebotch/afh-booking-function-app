namespace AFH.Acs.Recorder.DTOs;


public class AdviserDto
{
    public string AdviserId { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Region { get; set; } = default!;

    // Whether this adviser is part of the Lead Tech program
    public bool LeadTechFlag { get; set; }

    // Active/Inactive
    public bool ActiveFlag { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}