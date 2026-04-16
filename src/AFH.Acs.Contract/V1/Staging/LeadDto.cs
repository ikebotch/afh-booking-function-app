using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Acs.Recorder.DTOs;

public class LeadDto
{
    public string LeadId { get; set; } = default!;

    public string ClientId { get; set; } = default!;
    public string ClientName { get; set; } = default!;

    public string? ClientEmail { get; set; }

    public string SourceSystem { get; set; } = default!;   // e.g. “Snowflake”, “Salesforce”, etc.
    public string Status { get; set; } = default!;         // e.g. “Open”, “In Progress”, “Closed”

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}