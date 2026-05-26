using AFH.Booking.Function.Functions.V1.Docs;
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
    public void CreateOpenApiJson_ConfirmHoldIncludesTypedRequestSchema()
    {
        var json = BookingOpenApiDocumentFactory.CreateOpenApiJson(new Uri("https://localhost/api/openapi/v1.json"));
        var document = JsonNode.Parse(json)!.AsObject();
        var paths = document["paths"]!.AsObject();
        var schemas = document["components"]!["schemas"]!.AsObject();

        var confirmPost = paths["/v1/bookings/holds/{holdId}/confirm"]!["post"]!.AsObject();
        var description = confirmPost["description"]!.GetValue<string>();
        var requestBodyRequired = confirmPost["requestBody"]!["required"]!.GetValue<bool>();
        var requestBodySchemaRef = confirmPost["requestBody"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        var confirmSchema = schemas["ConfirmBookingRequest"]!.AsObject();
        var confirmProperties = confirmSchema["properties"]!.AsObject();
        var successSchemaRef = confirmPost["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        var errorSchemaRef = confirmPost["responses"]!["400"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();

        Assert.Contains("route holdId", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("body is optional", description, StringComparison.OrdinalIgnoreCase);
        Assert.False(requestBodyRequired);
        Assert.Equal("#/components/schemas/ConfirmBookingRequest", requestBodySchemaRef);
        Assert.Equal("#/components/schemas/ApiResponseOfConfirmBookingResponse", successSchemaRef);
        Assert.Equal("#/components/schemas/ApiResponseOfProblemDetailsDto", errorSchemaRef);
        Assert.True(confirmProperties.ContainsKey("bookingId"));
        Assert.True(confirmProperties.ContainsKey("notes"));
    }

    [Fact]
    public void CreateOpenApiJson_CreateHoldIncludesWrappedRequestAndResponseSchemas()
    {
        var json = BookingOpenApiDocumentFactory.CreateOpenApiJson(new Uri("https://localhost/api/openapi/v1.json"));
        var document = JsonNode.Parse(json)!.AsObject();
        var paths = document["paths"]!.AsObject();

        var createHoldPost = paths["/v1/bookings/hold"]!["post"]!.AsObject();
        var requestBodySchemaRef = createHoldPost["requestBody"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        var successSchemaRef = createHoldPost["responses"]!["201"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        var errorSchemaRef = createHoldPost["responses"]!["400"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();

        Assert.Equal("#/components/schemas/CreateHoldRequest", requestBodySchemaRef);
        Assert.Equal("#/components/schemas/ApiResponseOfCreateBookingResponse", successSchemaRef);
        Assert.Equal("#/components/schemas/ApiResponseOfProblemDetailsDto", errorSchemaRef);
    }

    [Fact]
    public void CreateOpenApiJson_AvailabilityIncludesNestedRequestAndResponseStructures()
    {
        var json = BookingOpenApiDocumentFactory.CreateOpenApiJson(new Uri("https://localhost/api/openapi/v1.json"));
        var document = JsonNode.Parse(json)!.AsObject();
        var paths = document["paths"]!.AsObject();
        var schemas = document["components"]!["schemas"]!.AsObject();

        var availabilityPost = paths["/v1/transactions/{transactionId}/availability"]!["post"]!.AsObject();
        var requestBodySchemaRef = availabilityPost["requestBody"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        var successSchemaRef = availabilityPost["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();

        var availabilityRequest = schemas["GetAvailabilityRequest"]!.AsObject();
        var availabilityRequestProperties = availabilityRequest["properties"]!.AsObject();
        var windowSchema = availabilityRequestProperties["window"]!.AsObject();
        var destinationSchema = availabilityRequestProperties["destinationAddress"]!.AsObject();
        var preferredAdviserIdsSchema = availabilityRequestProperties["preferredAdviserIds"]!.AsObject();

        var successSchema = schemas["ApiResponseOfGetAvailabilityResponse"]!.AsObject();
        var successDataSchema = successSchema["properties"]!["data"]!.AsObject();
        var advisersSchema = successDataSchema["properties"]!["advisers"]!.AsObject();
        var adviserItemSchema = advisersSchema["items"]!.AsObject();
        var slotsSchema = adviserItemSchema["properties"]!["slots"]!.AsObject();

        Assert.Equal("#/components/schemas/GetAvailabilityRequest", requestBodySchemaRef);
        Assert.Equal("#/components/schemas/ApiResponseOfGetAvailabilityResponse", successSchemaRef);
        Assert.Equal("object", windowSchema["type"]!.GetValue<string>());
        Assert.True(windowSchema["properties"]!["startUtc"] is not null);
        Assert.True(windowSchema["properties"]!["endUtc"] is not null);
        Assert.Equal("object", destinationSchema["type"]!.GetValue<string>());
        Assert.True(destinationSchema["properties"]!["line1"] is not null);
        Assert.True(destinationSchema["properties"]!["postcode"] is not null);
        Assert.Equal("array", preferredAdviserIdsSchema["type"]!.GetValue<string>());
        Assert.Equal("string", preferredAdviserIdsSchema["items"]!["type"]!.GetValue<string>());
        Assert.Equal("array", advisersSchema["type"]!.GetValue<string>());
        Assert.Equal("array", slotsSchema["type"]!.GetValue<string>());
    }

    [Fact]
    public void CreateOpenApiJson_GetBookingDetailsIncludesWrappedResponseSchema()
    {
        var json = BookingOpenApiDocumentFactory.CreateOpenApiJson(new Uri("https://localhost/api/openapi/v1.json"));
        var document = JsonNode.Parse(json)!.AsObject();
        var paths = document["paths"]!.AsObject();

        var bookingGet = paths["/v1/bookings/{bookingId}"]!["get"]!.AsObject();
        var successSchemaRef = bookingGet["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();

        Assert.Equal("#/components/schemas/ApiResponseOfBookingDetailsResponse", successSchemaRef);
    }

    [Fact]
    public void CreateOpenApiJson_SelfServiceRoutesDocumentTokenContractAndExamples()
    {
        var json = BookingOpenApiDocumentFactory.CreateOpenApiJson(new Uri("https://localhost/api/openapi/v1.json"));
        var document = JsonNode.Parse(json)!.AsObject();
        var paths = document["paths"]!.AsObject();
        var schemas = document["components"]!["schemas"]!.AsObject();

        var viewGet = paths["/v1/self-service/bookings/{bookingId}"]!["get"]!.AsObject();
        var cancelPost = paths["/v1/self-service/bookings/{bookingId}/cancel"]!["post"]!.AsObject();
        var optionsPost = paths["/v1/self-service/bookings/{bookingId}/rearrangement/options"]!["post"]!.AsObject();
        var rearrangePost = paths["/v1/self-service/bookings/{bookingId}/rearrange"]!["post"]!.AsObject();

        Assert.Equal("Self-Service Bookings", viewGet["tags"]![0]!.GetValue<string>());
        Assert.Equal("View booking by secure client token", viewGet["summary"]!.GetValue<string>());
        Assert.Contains("not the internal/admin booking details route", viewGet["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Invalid or expired tokens return 401", viewGet["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("different booking returns 403", viewGet["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        var tokenParameter = viewGet["parameters"]!.AsArray()
            .Select(x => x!.AsObject())
            .Single(x => x["name"]!.GetValue<string>() == "token");
        Assert.Equal("query", tokenParameter["in"]!.GetValue<string>());
        Assert.Equal("opaque-client-token", tokenParameter["example"]!.GetValue<string>());
        Assert.DoesNotContain(viewGet["parameters"]!.AsArray(), x => x!["name"]!.GetValue<string>() == "accessToken");

        var viewExample = viewGet["responses"]!["200"]!["content"]!["application/json"]!["example"]!.AsObject();
        var viewExampleData = viewExample["data"]!.AsObject();
        Assert.True(viewExampleData.ContainsKey("viewBookingUrl"));
        Assert.True(viewExampleData.ContainsKey("cancelBookingUrl"));
        Assert.True(viewExampleData.ContainsKey("rescheduleBookingUrl"));

        Assert.Equal("#/components/schemas/CancelBookingRequest", cancelPost["requestBody"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>());
        Assert.False(cancelPost["requestBody"]!["required"]!.GetValue<bool>());
        Assert.Equal("#/components/schemas/RearrangementOptionsRequest", optionsPost["requestBody"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>());
        Assert.Equal("#/components/schemas/RearrangeBookingRequest", rearrangePost["requestBody"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>());
        Assert.Equal("CLIENT_RESCHEDULE", rearrangePost["requestBody"]!["content"]!["application/json"]!["example"]!["reasonCode"]!.GetValue<string>());
        Assert.Contains("old token must not be reused", rearrangePost["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing, invalid, or expired", rearrangePost["responses"]!["401"]!["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not match the route booking", rearrangePost["responses"]!["403"]!["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        var bookingDetailsProperties = schemas["BookingDetailsResponse"]!["properties"]!.AsObject();
        Assert.True(bookingDetailsProperties.ContainsKey("viewBookingUrl"));
        Assert.True(bookingDetailsProperties.ContainsKey("cancelBookingUrl"));
        Assert.True(bookingDetailsProperties.ContainsKey("rescheduleBookingUrl"));
    }
}
