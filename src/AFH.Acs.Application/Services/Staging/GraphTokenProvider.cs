using AFH.Acs.Recorder.Models.V1;
using AFH.Acs.Recorder.Services.Interface;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AFH.Acs.Recorder.Services;

public class GraphTokenProvider : IGraphTokenProvider
{
    private readonly HttpClient _http;
    private readonly GraphSettings _settings;

    public GraphTokenProvider(HttpClient http, GraphSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    public async Task<string> GetTokenAsync()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _settings.BearerUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _settings.ClientId,
                ["client_secret"] = _settings.ClientSecret,
                ["grant_type"] = _settings.GrantType,
                ["scope"] = _settings.Scope
            })
        };

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("access_token").GetString()!;
    }
}
