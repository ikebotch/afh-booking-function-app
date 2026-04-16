namespace AFH.Acs.Recorder.DTOs;

public class LeadListItemDto
{
  
    public string LeadId { get; set; } = default!;

  
    public string ClientName { get; set; } = default!;

  
    public string Email { get; set; } = default!;


    public string? Region { get; set; }

    public bool IsActive { get; set; }

    public List<string> Tags { get; set; } = new();
}