using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;

namespace AFH.Booking.Functions.V1.Docs;

public sealed class ApiDocsFunction
{
    [Function("Booking_OpenApiV1")]
    public async Task<HttpResponseData> OpenApi(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "openapi/v1.json")] HttpRequestData req)
    {
        var baseUrl = $"{req.Url.Scheme}://{req.Url.Host}";
        if (!req.Url.IsDefaultPort)
            baseUrl += $":{req.Url.Port}";

        var doc = new Dictionary<string, object>
        {
            ["openapi"] = "3.0.1",
            ["info"] = new Dictionary<string, object>
            {
                ["title"] = "AFH Booking Service API",
                ["version"] = "v1",
                ["description"] =
                    "Booking orchestration across Leads, Location, Calendar service and ACS meeting links."
            },
            ["servers"] = new object[]
            {
                new Dictionary<string, object> { ["url"] = $"{baseUrl}/api" }
            },
            ["paths"] = BuildPaths(),
            ["components"] = BuildComponents()
        };

        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await res.WriteStringAsync(JsonSerializer.Serialize(doc, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true
        }));
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

    private static Dictionary<string, object> BuildPaths()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["/v1/transactions/{transactionId}/availability"] = new Dictionary<string, object>
            {
                ["post"] = new Dictionary<string, object>
                {
                    ["tags"] = new[] { "Availability" },
                    ["summary"] = "Get availability for a transaction",
                    ["parameters"] = new object[]
                    {
                        Parameter("transactionId", "path", true, "string")
                    },
                    ["requestBody"] = RequestBody("GetAvailabilityRequest", true),
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = Response("GetAvailabilityResponse"),
                        ["400"] = ProblemResponse(),
                        ["404"] = ProblemResponse()
                    }
                }
            },
            ["/v1/bookings/hold"] = new Dictionary<string, object>
            {
                ["post"] = new Dictionary<string, object>
                {
                    ["tags"] = new[] { "Bookings" },
                    ["summary"] = "Create booking hold",
                    ["requestBody"] = RequestBody("CreateHoldRequest", true),
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["201"] = Response("CreateBookingResponse"),
                        ["400"] = ProblemResponse(),
                        ["409"] = ProblemResponse()
                    }
                }
            },
            ["/v1/bookings/holds/{holdId}/confirm"] = new Dictionary<string, object>
            {
                ["post"] = new Dictionary<string, object>
                {
                    ["tags"] = new[] { "Bookings" },
                    ["summary"] = "Confirm booking",
                    ["parameters"] = new object[]
                    {
                        Parameter("holdId", "path", true, "string")
                    },
                    ["requestBody"] = RequestBody("ConfirmBookingRequest", false),
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = Response("ConfirmBookingResponse"),
                        ["400"] = ProblemResponse(),
                        ["404"] = ProblemResponse(),
                        ["409"] = ProblemResponse()
                    }
                }
            },
            ["/v1/bookings/{bookingId}/cancel"] = new Dictionary<string, object>
            {
                ["post"] = new Dictionary<string, object>
                {
                    ["tags"] = new[] { "Bookings" },
                    ["summary"] = "Cancel booking",
                    ["parameters"] = new object[]
                    {
                        Parameter("bookingId", "path", true, "string")
                    },
                    ["requestBody"] = RequestBody("CancelBookingRequest", false),
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = Response("CancelBookingResponse"),
                        ["400"] = ProblemResponse(),
                        ["404"] = ProblemResponse()
                    }
                }
            },
            ["/v1/clients/{transactionId}"] = new Dictionary<string, object>
            {
                ["get"] = new Dictionary<string, object>
                {
                    ["tags"] = new[] { "Clients" },
                    ["summary"] = "Get client details by transaction id",
                    ["parameters"] = new object[]
                    {
                        Parameter("transactionId", "path", true, "string")
                    },
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object> { ["description"] = "Client details response" },
                        ["404"] = ProblemResponse()
                    }
                }
            }
        };

    private static Dictionary<string, object> BuildComponents()
        => new()
        {
            ["schemas"] = new Dictionary<string, object>
            {
                ["GetAvailabilityRequest"] = OpenApiSchema.FromType(typeof(GetAvailabilityRequest)),
                ["GetAvailabilityResponse"] = OpenApiSchema.FromType(typeof(GetAvailabilityResponse)),
                ["CreateHoldRequest"] = OpenApiSchema.FromType(typeof(CreateHoldRequest)),
                ["CreateBookingResponse"] = OpenApiSchema.FromType(typeof(CreateBookingResponse)),
                ["ConfirmBookingRequest"] = OpenApiSchema.FromType(typeof(ConfirmBookingRequest)),
                ["ConfirmBookingResponse"] = OpenApiSchema.FromType(typeof(ConfirmBookingResponse)),
                ["CancelBookingRequest"] = OpenApiSchema.FromType(typeof(CancelBookingRequest)),
                ["CancelBookingResponse"] = OpenApiSchema.FromType(typeof(CancelBookingResponse))
            }
        };

    private static Dictionary<string, object> RequestBody(string schemaName, bool required)
        => new()
        {
            ["required"] = required,
            ["content"] = new Dictionary<string, object>
            {
                ["application/json"] = new Dictionary<string, object>
                {
                    ["schema"] = new Dictionary<string, object> { ["$ref"] = $"#/components/schemas/{schemaName}" }
                }
            }
        };

    private static Dictionary<string, object> Response(string schemaName)
        => new()
        {
            ["description"] = "Success",
            ["content"] = new Dictionary<string, object>
            {
                ["application/json"] = new Dictionary<string, object>
                {
                    ["schema"] = new Dictionary<string, object> { ["$ref"] = $"#/components/schemas/{schemaName}" }
                }
            }
        };

    private static Dictionary<string, object> ProblemResponse()
        => new()
        {
            ["description"] = "Problem",
            ["content"] = new Dictionary<string, object>
            {
                ["application/json"] = new Dictionary<string, object>
                {
                    ["schema"] = new Dictionary<string, object> { ["type"] = "object" }
                }
            }
        };

    private static Dictionary<string, object> Parameter(string name, string where, bool required, string type)
        => new()
        {
            ["name"] = name,
            ["in"] = where,
            ["required"] = required,
            ["schema"] = new Dictionary<string, object> { ["type"] = type }
        };
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
            return new Dictionary<string, object>
            {
                ["type"] = "string",
                ["enum"] = Enum.GetNames(unwrapped)
            };

        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(unwrapped) && unwrapped != typeof(string))
            return new Dictionary<string, object> { ["type"] = "array", ["items"] = new Dictionary<string, object> { ["type"] = "object" } };

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
