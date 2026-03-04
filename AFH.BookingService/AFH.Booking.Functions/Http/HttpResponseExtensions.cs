using System.Net;
using AFH.Booking.Functions.Configuration;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Functions.Http;

public static class HttpResponseExtensions
{
    public static async Task<HttpResponseData> OkJsonAsync<T>(
        this HttpRequestData req,
        T body,
        CancellationToken ct)
    {
        var res = req.CreateResponse(HttpStatusCode.OK);

        // avoid the "Content-Type does not support multiple values" issue
        res.Headers.Remove("Content-Type");

        await res.WriteAsJsonAsync(
            body,
            Json.Serializer,
            contentType: "application/json; charset=utf-8",
            cancellationToken: ct);

        return res;
    }

    public static async Task<HttpResponseData> ProblemAsync(
        this HttpRequestData req,
        HttpStatusCode status,
        string message,
        CancellationToken ct,
        string? code = null)
    {
        var res = req.CreateResponse(status);

        res.Headers.Remove("Content-Type");

        var payload = new
        {
            error = new
            {
                code = code ?? status.ToString(),
                message
            }
        };

        await res.WriteAsJsonAsync(
            payload,
            Json.Serializer,
            contentType: "application/json; charset=utf-8",
            cancellationToken: ct);

        return res;
    }
}