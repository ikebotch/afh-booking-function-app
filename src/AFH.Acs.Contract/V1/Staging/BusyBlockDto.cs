using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Acs.Recorder.DTOs;

public class BusyBlockDto
{
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
}
