using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core.Serialization;

namespace AFH.Booking.Functions.Configuration;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static readonly JsonObjectSerializer Serializer = new(Options);
}