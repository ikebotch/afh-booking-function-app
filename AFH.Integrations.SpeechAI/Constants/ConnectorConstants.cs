using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Integrations.SpeechAI.Constants
{
    public static class ConnectorConstants
    {
        public static class HeaderConstants
        {
            public const string SubscriptionKey = "Ocp-Apim-Subscription-Key";
            public const string MediaTypeJson = "application/json";
        }

        public static class Endpoints
        {
            public const string Base = "speechtotext/v3.1/transcriptions";
            public const string StatusCheck = Base + "/{0}";
            public const string GetTranscriptionFiles = Base + "/{0}/files";

        }
        public static class ErrorMessageConstants
        {
            public const string APIFailed = "API call failed: {0} \n{1}";
            public const string APIFailedDetailed = "Call to API failed: \n{0}\n{1}\n\nRequest made: \n{2}\n\n";
        }
    }
}
