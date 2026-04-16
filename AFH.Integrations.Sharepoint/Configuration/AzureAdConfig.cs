using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Integrations.Sharepoint.Configuration
{
	public class AzureAdConfig
	{
		public string TenantId { get; set; }
		public string GrantType { get; set; }
		public string ClientId {  get; set; }
		public string ClientSecret {  get; set; }
		public string Scope {  get; set; }

	}
}
