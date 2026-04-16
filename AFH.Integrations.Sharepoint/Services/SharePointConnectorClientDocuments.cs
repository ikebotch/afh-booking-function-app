using Microsoft.Graph.DeviceManagement.Reports.RetrieveDeviceAppInstallationStatusReport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Integrations.Sharepoint.Services
{
    public partial class SharepointService
    {

        public async Task<bool> ClientDocumentVerifiyListConfig()
        {
            var siteId = Environment.GetEnvironmentVariable("ClientDocumentSiteId");
            var clientList = Environment.GetEnvironmentVariable("ClientDocumentsClientList");
            var config_list = Environment.GetEnvironmentVariable("ClientDocumentConfigList");

            var clientListExists = _sharepointConnector.ListExists(siteId, clientList).GetAwaiter().GetResult();
            var configListExists = _sharepointConnector.ListExists(siteId, config_list).GetAwaiter().GetResult();
            if (!clientListExists || !configListExists)
            {
                return false;
            }

            return true;
        }
    }
}
