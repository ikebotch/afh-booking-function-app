using System.Reflection;

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
    private static readonly NullabilityInfoContext NullabilityContext = new();

    public static string GetSchemaName(Type type)
    {
        var unwrapped = Nullable.GetUnderlyingType(type) ?? type;

        if (!unwrapped.IsGenericType)
        {
            var schemaName = unwrapped.Name;
            if (unwrapped.Namespace?.Contains(".Contracts.V2.", StringComparison.OrdinalIgnoreCase) == true)
                schemaName += "V2";

            return schemaName;
        }

        var genericName = unwrapped.Name[..unwrapped.Name.IndexOf('`')];
        var argumentNames = string.Join("And", unwrapped.GetGenericArguments().Select(GetSchemaName));
        return $"{genericName}Of{argumentNames}";
    }

    public static Dictionary<string, object> FromType(Type type)
    {
        return BuildSchema(type);
    }

    private static Dictionary<string, object> BuildSchema(Type type)
    {
        var unwrapped = Nullable.GetUnderlyingType(type) ?? type;

        if (unwrapped == typeof(string))
            return new Dictionary<string, object> { ["type"] = "string" };
        if (unwrapped == typeof(bool))
            return new Dictionary<string, object> { ["type"] = "boolean" };
        if (unwrapped == typeof(int) || unwrapped == typeof(long) || unwrapped == typeof(short))
            return new Dictionary<string, object> { ["type"] = "integer" };
        if (unwrapped == typeof(float) || unwrapped == typeof(double) || unwrapped == typeof(decimal))
            return new Dictionary<string, object> { ["type"] = "number" };
        if (unwrapped == typeof(DateTime) || unwrapped == typeof(DateTimeOffset))
            return new Dictionary<string, object> { ["type"] = "string", ["format"] = "date-time" };
        if (unwrapped == typeof(DateOnly))
            return new Dictionary<string, object> { ["type"] = "string", ["format"] = "date" };
        if (unwrapped == typeof(TimeSpan))
            return new Dictionary<string, object> { ["type"] = "string", ["format"] = "duration" };
        if (unwrapped == typeof(Guid))
            return new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" };
        if (unwrapped == typeof(object))
            return new Dictionary<string, object> { ["type"] = "object" };

        if (unwrapped.IsEnum)
        {
            return new Dictionary<string, object>
            {
                ["type"] = "string",
                ["enum"] = Enum.GetNames(unwrapped)
            };
        }

        if (TryGetDictionaryValueType(unwrapped, out var valueType))
        {
            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["additionalProperties"] = BuildSchema(valueType)
            };
        }

        if (TryGetEnumerableElementType(unwrapped, out var elementType))
        {
            return new Dictionary<string, object>
            {
                ["type"] = "array",
                ["items"] = BuildSchema(elementType)
            };
        }

        var properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var required = new List<string>();

        var props = unwrapped
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.GetMethod is not null && p.GetMethod.IsPublic && p.GetIndexParameters().Length == 0);

        foreach (var prop in props)
        {
            var propertyName = ResolvePropertyName(prop);
            properties[propertyName] = BuildSchema(prop.PropertyType);

            if (IsRequired(prop))
                required.Add(propertyName);
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties
        };

        if (required.Count > 0)
            schema["required"] = required;

        return schema;
    }

    private static bool TryGetEnumerableElementType(Type type, out Type elementType)
    {
        if (type == typeof(string))
        {
            elementType = typeof(string);
            return false;
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        var enumerableType = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableType is not null)
        {
            elementType = enumerableType.GetGenericArguments()[0];
            return true;
        }

        elementType = typeof(object);
        return false;
    }

    private static bool TryGetDictionaryValueType(Type type, out Type valueType)
    {
        var dictionaryType = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>)
            ? type
            : type.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IDictionary<,>));

        if (dictionaryType is not null && dictionaryType.GetGenericArguments()[0] == typeof(string))
        {
            valueType = dictionaryType.GetGenericArguments()[1];
            return true;
        }

        valueType = typeof(object);
        return false;
    }

    private static string ResolvePropertyName(PropertyInfo property)
    {
        var jsonName = property.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>()?.Name;
        if (!string.IsNullOrWhiteSpace(jsonName))
            return jsonName;

        return char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
    }

    private static bool IsRequired(PropertyInfo property)
    {
        var propertyType = property.PropertyType;
        if (Nullable.GetUnderlyingType(propertyType) is not null)
            return false;

        if (propertyType.IsValueType)
            return true;

        var nullability = NullabilityContext.Create(property);
        return nullability.WriteState == NullabilityState.NotNull || nullability.ReadState == NullabilityState.NotNull;
    }
}
