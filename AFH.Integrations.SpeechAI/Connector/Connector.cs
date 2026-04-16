using AFH.Integrations.SpeechAI.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using static AFH.Integrations.SpeechAI.Constants.ConnectorConstants;

namespace AFH.Integrations.SpeechAI
{
    public class Connector
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerSettings _serializerSettings;
        private readonly IOptions<SpeechAIConfig> _options;

        public Connector(IOptions<SpeechAIConfig> options)
        {
			_options = options;
			_httpClient = new HttpClient
            {
                BaseAddress = new Uri(_options.Value.BaseUrl?.TrimEnd('/'))
            };

            _httpClient.DefaultRequestHeaders.Add(HeaderConstants.SubscriptionKey, _options.Value.SubscriptionKey);

            _serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        public async Task<T> PostAsync<T>(string endpoint, HttpContent content)
        {
            var response = await _httpClient.PostAsync(endpoint, content);

            return await HandleApiCall<T>(response);
        }

        public async Task<T> GetAsync<T>(string endpoint, string args = null)
        {
            var requestUrl = endpoint + (!string.IsNullOrWhiteSpace(args) ? "?" + args : "");
            var response = await _httpClient.GetAsync(requestUrl);
            return await HandleApiCall<T>(response, requestUrl);
        }
        private async Task<T> HandleApiCall<T>(HttpResponseMessage response, string requestUrl = null)
        {
            var result = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(string.Format(ErrorMessageConstants.APIFailedDetailed, response.ReasonPhrase, result, requestUrl));
            }

            if (typeof(T) == typeof(string))
            {
                return (T)(object)result;
            }
            return JsonConvert.DeserializeObject<T>(result, _serializerSettings);
        }

    }
}
