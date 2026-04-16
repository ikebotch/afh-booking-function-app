using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Integrations.SpeechAI.Models.Responses
{
    public class JobFilesResponse
    {

        [JsonProperty("values")]
        public IEnumerable<JobFileResponse> Items { get; set; }
    }

    public class JobFileResponse
    {
        [JsonProperty("self")]
        public string JobUrl { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("links")]
        public JobFileLinkResponse File { get; set; }
    }

    public class JobFileLinkResponse
    {
        [JsonProperty("contentUrl")]
        public string Url {  get; set; }
    }
}
