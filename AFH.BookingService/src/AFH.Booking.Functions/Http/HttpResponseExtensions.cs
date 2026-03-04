namespace AFH.Booking.Functions.Http;

public static class HttpResponseExtensions
{
    public static async Task<HttpResponseData> OkJsonAsync<T>(
        this HttpRequestData req,
        T body,
        CancellationToken ct)
    {
        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(ApiResponse<T>.Ok(body), cancellationToken: ct);
        return res;
    }

    public static async Task<HttpResponseData> CreatedJsonAsync<T>(
        this HttpRequestData req,
        T body,
        CancellationToken ct)
    {
        var res = req.CreateResponse(HttpStatusCode.Created);
        await res.WriteAsJsonAsync(ApiResponse<T>.Ok(body), cancellationToken: ct);
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

        var payload = ApiResponse<object>.Fail(
            message: message,
            code: code ?? status.ToString());

        await res.WriteAsJsonAsync(payload, cancellationToken: ct);
        return res;
    }
}