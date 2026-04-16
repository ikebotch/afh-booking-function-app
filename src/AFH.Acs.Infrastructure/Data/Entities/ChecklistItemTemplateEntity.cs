using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Acs.Recorder.Infrastructure.Data.Entities;
public class ChecklistItemTemplateEntity
{
    public string TemplateId { get; set; } = default!;
    public string ItemId { get; set; } = default!;

    public string DisplayText { get; set; } = default!;
    public int DisplayOrder { get; set; }

    public ChecklistTemplateEntity Template { get; set; } = default!;
}