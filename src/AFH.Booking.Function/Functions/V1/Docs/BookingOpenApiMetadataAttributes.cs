using System.Net;

namespace AFH.Booking.Function.Functions.V1.Docs;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
internal sealed class BookingOpenApiOperationAttribute : Attribute
{
    public BookingOpenApiOperationAttribute(string tag, string summary)
    {
        Tag = tag;
        Summary = summary;
    }

    public string Tag { get; }
    public string Summary { get; }
    public string? Description { get; init; }
    public string? HttpMethod { get; init; }
    public Type? RequestBodyType { get; init; }
    public Type? ResponseType { get; init; }
    public HttpStatusCode SuccessStatusCode { get; init; } = HttpStatusCode.OK;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
internal sealed class BookingOpenApiTagAttribute : Attribute
{
    public BookingOpenApiTagAttribute(string tag)
    {
        Tag = tag;
    }

    public string Tag { get; }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class BookingOpenApiQueryParameterAttribute : Attribute
{
    public BookingOpenApiQueryParameterAttribute(string name, string type)
    {
        Name = name;
        Type = type;
    }

    public string Name { get; }
    public string Type { get; }
    public bool IsRequired { get; init; }
    public string? Description { get; init; }
    public string? Format { get; init; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
internal sealed class BookingOpenApiExcludeAttribute : Attribute;
