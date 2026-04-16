using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Acs.Recorder.Models.V1;

public class SharePointConfigModel
{
    public string ListId { get; set; }

    public string SiteId { get; set; }

}
//public class AdvisorListConfigModel : SharePointConfigModel;
//public class ClientListConfigModel : SharePointConfigModel;

public class SharePointConfigWrapper
{
    public SharePointConfigModel AdvisorListConfigs { get; set; }
    public SharePointConfigModel ClientTranscriptionListConfigs { get; set; }
    public SharePointConfigModel ClientOverviewListConfigs { get; set; }
}