namespace AFH.Booking.Functions.Http;

public static class HttpResponseExtensions
{
    public static ApiPaging SinglePage(int totalItems) => new()
    {
        Page = 1,
        PageSize = totalItems,
        TotalItems = totalItems,
        TotalPages = totalItems == 0 ? 0 : 1
    };

    public static async Task<HttpResponseData> OkJsonAsync<T>(
        this HttpRequestData req,
        T body,
        CancellationToken ct,
        ApiPaging? paging = null)
    {
        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(ApiResponse<T>.Ok(body, paging), cancellationToken: ct);
        return res;
    }

    public static async Task<HttpResponseData> CreatedJsonAsync<T>(
        this HttpRequestData req,
        T body,
        CancellationToken ct,
        ApiPaging? paging = null)
    {
        var res = req.CreateResponse(HttpStatusCode.Created);
        await res.WriteAsJsonAsync(ApiResponse<T>.Ok(body, paging), cancellationToken: ct);
        return res;
    }

    public static async Task<HttpResponseData> AcceptedJsonAsync<T>(
        this HttpRequestData req,
        T body,
        CancellationToken ct,
        ApiPaging? paging = null)
    {
        var res = req.CreateResponse(HttpStatusCode.Accepted);
        await res.WriteAsJsonAsync(ApiResponse<T>.Ok(body, paging), cancellationToken: ct);
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

        var payload = ApiResponse<object>.Fail(new
        {
            code = code ?? status.ToString(),
            message
        });

        await res.WriteAsJsonAsync(payload, cancellationToken: ct);
        return res;
    }
}
