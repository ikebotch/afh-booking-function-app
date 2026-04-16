using AFH.Acs.Function.Http;
using Microsoft.Azure.Functions.Worker.Http;
using System.Text.Json;
using System.Web;

namespace AFH.Acs.Function.Http;

public static class RequestParsingExtensions
{
    public static async Task<T?> ReadJsonAsync<T>(this HttpRequestData req, CancellationToken ct)
    {
        return await JsonSerializer.DeserializeAsync<T>(req.Body, Json.Options, ct);
    }

    public static string? Query(this HttpRequestData req, string key)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        return query[key];
    }


    public static IReadOnlyList<string> QueryMany(this HttpRequestData req, string key)
    {
        var qs = HttpUtility.ParseQueryString(req.Url.Query);
        var values = qs.GetValues(key);
        return values is null ? Array.Empty<string>() : values.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
    }
}
