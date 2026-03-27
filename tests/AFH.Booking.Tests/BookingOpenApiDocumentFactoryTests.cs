using AFH.Booking.Functions.V1.Docs;
using System.Text.Json.Nodes;

namespace AFH.Booking.Tests;

public class BookingOpenApiDocumentFactoryTests
{
    [Fact]
    public void CreateOpenApiJson_IncludesRoutesMissingFromManualDocAndUsesExplicitTags()
    {
        var json = BookingOpenApiDocumentFactory.CreateOpenApiJson(new Uri("https://localhost/api/openapi/v1.json"));
        var document = JsonNode.Parse(json)!.AsObject();
        var paths = document["paths"]!.AsObject();

        Assert.True(paths.ContainsKey("/v2/transactions/{transactionId}/availability"));
        Assert.True(paths.ContainsKey("/v1/me"));
        Assert.True(paths.ContainsKey("/v1/admin/advisers/projection/feed"));

        var availabilityTag = paths["/v2/transactions/{transactionId}/availability"]!["post"]!["tags"]![0]!.GetValue<string>();
        var usersTag = paths["/v1/me"]!["get"]!["tags"]![0]!.GetValue<string>();
        var adminTag = paths["/v1/admin/advisers/projection/feed"]!["get"]!["tags"]![0]!.GetValue<string>();

        Assert.Equal("Availability", availabilityTag);
        Assert.Equal("Users", usersTag);
        Assert.Equal("Internal/Admin", adminTag);
    }

    [Fact]
    public void CreateOpenApiJson_IncludesExplicitQueryParametersForProjectionEndpoints()
    {
        var json = BookingOpenApiDocumentFactory.CreateOpenApiJson(new Uri("https://localhost/api/openapi/v1.json"));
        var document = JsonNode.Parse(json)!.AsObject();
        var operation = document["paths"]!["/v1/calendar/users/{userId}/schedule"]!["get"]!.AsObject();
        var parameters = operation["parameters"]!.AsArray();

        Assert.Contains(parameters, parameter => parameter?["name"]?.GetValue<string>() == "startUtc" && parameter["in"]?.GetValue<string>() == "query");
        Assert.Contains(parameters, parameter => parameter?["name"]?.GetValue<string>() == "endUtc" && parameter["in"]?.GetValue<string>() == "query");
    }
}
