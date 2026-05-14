using AFH.Booking.Contracts.V1.Common;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Http;
using System.Reflection;

namespace AFH.Booking.Function.Functions.V1.Docs;

internal static class BookingOpenApiDocumentFactory
{
    private static readonly Type ProblemResponseType = typeof(ApiResponse<>).MakeGenericType(typeof(ProblemDetailsDto));

    public static string CreateOpenApiJson(Uri requestUrl)
    {
        var baseUrl = $"{requestUrl.Scheme}://{requestUrl.Host}";
        if (!requestUrl.IsDefaultPort)
            baseUrl += $":{requestUrl.Port}";

        var schemaTypes = new HashSet<Type> { ProblemResponseType };
        var paths = BuildPaths(schemaTypes);

        var doc = new Dictionary<string, object>
        {
            ["openapi"] = "3.0.1",
            ["info"] = new Dictionary<string, object>
            {
                ["title"] = "AFH Booking Service API",
                ["version"] = "v1",
                ["description"] = "Booking orchestration across Leads, Location, Calendar service and ACS meeting links."
            },
            ["servers"] = new object[]
            {
                new Dictionary<string, object> { ["url"] = $"{baseUrl}/api" }
            },
            ["paths"] = paths,
            ["components"] = new Dictionary<string, object>
            {
                ["schemas"] = schemaTypes
                    .Distinct()
                    .OrderBy(OpenApiSchema.GetSchemaName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => OpenApiSchema.GetSchemaName(x), x => (object)OpenApiSchema.FromType(x), StringComparer.OrdinalIgnoreCase)
            }
        };

        return JsonSerializer.Serialize(doc, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true
        });
    }

    private static Dictionary<string, object> BuildPaths(ISet<Type> schemaTypes)
    {
        var paths = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in typeof(BookingOpenApiDocumentFactory).Assembly.GetTypes().OrderBy(x => x.FullName, StringComparer.Ordinal))
        {
            if (type.GetCustomAttribute<BookingOpenApiExcludeAttribute>(inherit: false) is not null)
                continue;

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.GetCustomAttribute<FunctionAttribute>(inherit: false) is null)
                    continue;

                if (method.GetCustomAttribute<BookingOpenApiExcludeAttribute>(inherit: false) is not null)
                    continue;

                var httpTrigger = method.GetParameters()
                    .SelectMany(parameter => parameter.GetCustomAttributes<HttpTriggerAttribute>(inherit: false))
                    .SingleOrDefault();

                if (httpTrigger is null)
                    continue;

                var route = "/" + (httpTrigger.Route?.TrimStart('/') ?? string.Empty);
                if (!paths.TryGetValue(route, out var pathItemObj))
                {
                    pathItemObj = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    paths[route] = pathItemObj;
                }

                var pathItem = (Dictionary<string, object>)pathItemObj;
                var metadata = ResolveOperationMetadata(type, method);

                foreach (var httpMethod in httpTrigger.Methods.Select(x => x.ToLowerInvariant()))
                {
                    var operation = ResolveMetadataForHttpMethod(metadata, httpMethod);
                    var parameters = BuildParameters(route, method);

                    if (operation.RequestBodyType is not null)
                        schemaTypes.Add(operation.RequestBodyType);

                    if (operation.ResponseType is not null)
                        schemaTypes.Add(GetSuccessEnvelopeType(operation.ResponseType));

                    pathItem[httpMethod] = BuildOperation(httpMethod, operation, parameters);
                }
            }
        }

        return paths;
    }

    private static Dictionary<string, object> BuildOperation(
        string httpMethod,
        BookingOpenApiOperationAttribute operation,
        IReadOnlyList<object> parameters)
    {
        var value = new Dictionary<string, object>
        {
            ["tags"] = new[] { operation.Tag },
            ["summary"] = operation.Summary,
            ["responses"] = BuildResponses(operation)
        };

        if (!string.IsNullOrWhiteSpace(operation.Description))
            value["description"] = operation.Description;

        if (parameters.Count > 0)
            value["parameters"] = parameters;

        if (operation.RequestBodyType is not null && httpMethod is "post" or "put" or "patch")
        {
            value["requestBody"] = new Dictionary<string, object>
            {
                ["required"] = operation.RequestBodyRequired,
                ["content"] = new Dictionary<string, object>
                {
                    ["application/json"] = new Dictionary<string, object>
                    {
                        ["schema"] = SchemaRef(operation.RequestBodyType)
                    }
                }
            };
        }

        return value;
    }

    private static Dictionary<string, object> BuildResponses(BookingOpenApiOperationAttribute operation)
    {
        var responses = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            [((int)operation.SuccessStatusCode).ToString()] = operation.ResponseType is null
                ? new Dictionary<string, object> { ["description"] = "Success" }
                : new Dictionary<string, object>
                {
                    ["description"] = "Success",
                    ["content"] = new Dictionary<string, object>
                    {
                        ["application/json"] = new Dictionary<string, object>
                        {
                            ["schema"] = SchemaRef(GetSuccessEnvelopeType(operation.ResponseType))
                        }
                    }
                }
        };

        responses["400"] = ProblemResponse();
        responses["401"] = ProblemResponse();
        responses["403"] = ProblemResponse();
        responses["500"] = ProblemResponse();
        return responses;
    }

    private static IReadOnlyList<object> BuildParameters(string route, MethodInfo method)
    {
        var parameters = route.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => segment.StartsWith('{') && segment.EndsWith('}'))
            .Select(segment => (object)new Dictionary<string, object>
            {
                ["name"] = segment[1..^1],
                ["in"] = "path",
                ["required"] = true,
                ["schema"] = new Dictionary<string, object> { ["type"] = "string" }
            })
            .ToList();

        foreach (var query in method.GetCustomAttributes<BookingOpenApiQueryParameterAttribute>(inherit: false))
        {
            var schema = new Dictionary<string, object>
            {
                ["type"] = query.Type
            };

            if (!string.IsNullOrWhiteSpace(query.Format))
                schema["format"] = query.Format!;

            var parameter = new Dictionary<string, object>
            {
                ["name"] = query.Name,
                ["in"] = "query",
                ["required"] = query.IsRequired,
                ["schema"] = schema
            };

            if (!string.IsNullOrWhiteSpace(query.Description))
                parameter["description"] = query.Description!;

            parameters.Add(parameter);
        }

        return parameters;
    }

    private static BookingOpenApiOperationAttribute ResolveMetadataForHttpMethod(
        IReadOnlyList<BookingOpenApiOperationAttribute> metadata,
        string httpMethod)
        => metadata.FirstOrDefault(x => string.Equals(x.HttpMethod, httpMethod, StringComparison.OrdinalIgnoreCase))
            ?? metadata.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.HttpMethod))
            ?? new BookingOpenApiOperationAttribute(DeriveTag(string.Empty, httpMethod), Humanize(httpMethod));

    private static IReadOnlyList<BookingOpenApiOperationAttribute> ResolveOperationMetadata(Type type, MethodInfo method)
    {
        var methodMetadata = method.GetCustomAttributes<BookingOpenApiOperationAttribute>(inherit: false).ToArray();
        if (methodMetadata.Length > 0)
            return methodMetadata;

        var typeMetadata = type.GetCustomAttributes<BookingOpenApiOperationAttribute>(inherit: false).ToArray();
        if (typeMetadata.Length > 0)
            return typeMetadata;

        var explicitTag = method.GetCustomAttribute<BookingOpenApiTagAttribute>(inherit: false)?.Tag
            ?? type.GetCustomAttribute<BookingOpenApiTagAttribute>(inherit: false)?.Tag;
        var inferredTag = explicitTag ?? DeriveTag(type.Namespace ?? string.Empty, type.Name);
        var inferredSummary = Humanize(type.Name.Replace("Function", string.Empty, StringComparison.OrdinalIgnoreCase));
        return [new BookingOpenApiOperationAttribute(inferredTag, inferredSummary)];
    }

    private static string DeriveTag(string source, string fallback)
    {
        if (source.Contains(".Admin", StringComparison.OrdinalIgnoreCase))
            return "Internal/Admin";
        if (source.Contains(".Availability", StringComparison.OrdinalIgnoreCase))
            return "Availability";
        if (source.Contains(".Calendar", StringComparison.OrdinalIgnoreCase))
            return "Calendar";
        if (source.Contains(".Clients", StringComparison.OrdinalIgnoreCase))
            return "Clients";
        if (source.Contains(".Users", StringComparison.OrdinalIgnoreCase))
            return "Users";
        if (fallback.Contains("Approval", StringComparison.OrdinalIgnoreCase))
            return "Approvals";
        if (fallback.Contains("Notification", StringComparison.OrdinalIgnoreCase) ||
            fallback.Contains("Bounce", StringComparison.OrdinalIgnoreCase))
            return "Notifications";
        if (fallback.Contains("Health", StringComparison.OrdinalIgnoreCase))
            return "Health";

        return "Bookings";
    }

    private static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Operation";

        var buffer = new List<char>(value.Length + 4);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (i > 0 && char.IsUpper(current) && !char.IsUpper(value[i - 1]))
                buffer.Add(' ');

            buffer.Add(current);
        }

        return new string(buffer.ToArray()).Trim();
    }

    private static Dictionary<string, object> ProblemResponse()
        => new()
        {
            ["description"] = "Problem",
            ["content"] = new Dictionary<string, object>
            {
                ["application/json"] = new Dictionary<string, object>
                {
                    ["schema"] = SchemaRef(ProblemResponseType)
                }
            }
        };

    private static Type GetSuccessEnvelopeType(Type responseType)
        => typeof(ApiResponse<>).MakeGenericType(responseType);

    private static Dictionary<string, object> SchemaRef(Type dtoType)
        => new() { ["$ref"] = $"#/components/schemas/{OpenApiSchema.GetSchemaName(dtoType)}" };
}