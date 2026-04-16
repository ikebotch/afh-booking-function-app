using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Acs.Recorder.DTOs;

public sealed class TranscriptionRunResult
{
    public string RecordingId { get; set; } = default!;
    public string? Transcript { get; set; }
    public bool Succeeded { get; set; }
    public string? FailureReason { get; set; }
}