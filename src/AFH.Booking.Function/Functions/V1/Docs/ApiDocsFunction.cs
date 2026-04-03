namespace AFH.Booking.Function.Functions.V1.Docs;

[BookingOpenApiExclude]
public sealed class ApiDocsFunction
{
    [Function("Booking_OpenApiV1")]
    public async Task<HttpResponseData> OpenApi(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "openapi/v1.json")] HttpRequestData req)
    {
        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await res.WriteStringAsync(BookingOpenApiDocumentFactory.CreateOpenApiJson(req.Url));
        return res;
    }

    [Function("Booking_ScalarUi")]
    public async Task<HttpResponseData> Scalar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scalar")] HttpRequestData req)
    {
        var html = """
                   <!doctype html>
                   <html lang="en">
                   <head>
                     <meta charset="utf-8" />
                     <meta name="viewport" content="width=device-width,initial-scale=1" />
                     <title>AFH Booking API Docs</title>
                   </head>
                   <body>
                     <script id="api-reference" data-url="/api/openapi/v1.json"></script>
                     <script src="https://cdn.jsdelivr.net/npm/@scalar/api-reference"></script>
                   </body>
                   </html>
                   """;

        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "text/html; charset=utf-8");
        await res.WriteStringAsync(html);
        return res;
    }
}

internal static class OpenApiSchema
{
    public static Dictionary<string, object> FromType(Type type)
    {
        var unwrapped = Nullable.GetUnderlyingType(type) ?? type;

        if (unwrapped == typeof(string))
            return new Dictionary<string, object> { ["type"] = "string" };
        if (unwrapped == typeof(bool))
            return new Dictionary<string, object> { ["type"] = "boolean" };
        if (unwrapped == typeof(int) || unwrapped == typeof(long))
            return new Dictionary<string, object> { ["type"] = "integer" };
        if (unwrapped == typeof(float) || unwrapped == typeof(double) || unwrapped == typeof(decimal))
            return new Dictionary<string, object> { ["type"] = "number" };
        if (unwrapped == typeof(DateTime) || unwrapped == typeof(DateTimeOffset))
            return new Dictionary<string, object> { ["type"] = "string", ["format"] = "date-time" };
        if (unwrapped == typeof(TimeSpan))
            return new Dictionary<string, object> { ["type"] = "string", ["format"] = "duration" };

        if (unwrapped.IsEnum)
        {
            return new Dictionary<string, object>
            {
                ["type"] = "string",
                ["enum"] = Enum.GetNames(unwrapped)
            };
        }

        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(unwrapped) && unwrapped != typeof(string))
        {
            return new Dictionary<string, object>
            {
                ["type"] = "array",
                ["items"] = new Dictionary<string, object> { ["type"] = "object" }
            };
        }

        var props = unwrapped
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .ToDictionary(
                p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..],
                p => (object)FromType(p.PropertyType),
                StringComparer.OrdinalIgnoreCase);

        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = props
        };
    }
}
