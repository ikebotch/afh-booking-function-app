using AFH.Integrations.SpeechAI.Models.Base;
using Newtonsoft.Json;

namespace AFH.Integrations.SpeechAI.Models.Responses
{
    public class JobStatusResponse : BaseSpeechModel
    {
        [JsonProperty("self")]
        public string JobUrl { get; set; }

        [JsonProperty("createdDateTime")]
        public DateTime Created { get; set; }

        [JsonProperty("lastActionDateTime")]
        public DateTime LastAction { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }
        [JsonProperty("failureReason")]
        public string FailureReason { get; set; }
        

        [JsonProperty("model")]
        public JobStatusResponseModel ModelObject { get; set; }

        [JsonProperty("links")]
        public JobStatusResponseLink Link { get; set; }

        public string JobId { get { return JobUrl.Split('/')?.Last(); } }

    }

    public class JobStatusResponseModel
    {
        [JsonProperty("self")]
        public string ModelUrl { get; set; }
    }

    public class JobStatusResponseLink
    {
        [JsonProperty("files")]
        public string FileUrl { get; set; }
    }
}
