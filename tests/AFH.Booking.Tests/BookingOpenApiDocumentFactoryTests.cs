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
        var availabilityV2Post = paths["/v2/transactions/{transactionId}/availability"]!["post"]!.AsObject();
        var requestBodySchemaRef = availabilityPost["requestBody"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        var successSchemaRef = availabilityPost["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        var description = availabilityPost["description"]!.GetValue<string>();
        var v2Description = availabilityV2Post["description"]!.GetValue<string>();
        var requestExample = availabilityPost["requestBody"]!["content"]!["application/json"]!["example"]!.AsObject();
        var responseExampleSlot = availabilityPost["responses"]!["200"]!["content"]!["application/json"]!["example"]!["data"]!["advisers"]![0]!["slots"]![0]!.AsObject();
        var v2ResponseExampleSlot = availabilityV2Post["responses"]!["200"]!["content"]!["application/json"]!["example"]!["data"]!["items"]![0]!["slots"]![0]!.AsObject();

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
        Assert.Contains("Sprint 5 availability governance", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("working pattern", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("minimum duration", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("capacity", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("revalidated again during hold creation", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sprint 5 availability governance", v2Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("S1 2HH", requestExample["destinationAddress"]!["postcode"]!.GetValue<string>());
        Assert.Equal("Pensions", requestExample["requiredSkills"]![0]!.GetValue<string>());
        Assert.Equal(1, responseExampleSlot["scoreBreakdown"]!["rule.workingPatternAllowed"]!.GetValue<int>());
        Assert.Equal(1, responseExampleSlot["scoreBreakdown"]!["rule.capacityAllowed"]!.GetValue<int>());
        Assert.Equal(1, responseExampleSlot["scoreBreakdown"]!["rule.minimumDurationAllowed"]!.GetValue<int>());
        Assert.Equal(1, v2ResponseExampleSlot["scoreBreakdown"]!["rule.capacityAllowed"]!.GetValue<int>());
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
        Assert.False(paths.ContainsKey("/v1/self-service/bookings/{bookingId}/hold"));

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
        Assert.Contains("current existing booking id", rearrangePost["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("newSlotId", rearrangePost["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No self-service hold endpoint exists", rearrangePost["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SlotNoLongerAvailable", rearrangePost["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing, invalid, or expired", rearrangePost["responses"]!["401"]!["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not match the route booking", rearrangePost["responses"]!["403"]!["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        var rearrangeRequestProperties = schemas["RearrangeBookingRequest"]!["properties"]!.AsObject();
        Assert.True(rearrangeRequestProperties.ContainsKey("newSlotId"));
        Assert.False(rearrangeRequestProperties.ContainsKey("newSlotTransactionId"));

        var optionsProperties = schemas["RearrangementOptionsResponse"]!["properties"]!.AsObject();
        Assert.True(optionsProperties.ContainsKey("transactionId"));

        var bookingDetailsProperties = schemas["BookingDetailsResponse"]!["properties"]!.AsObject();
        Assert.True(bookingDetailsProperties.ContainsKey("viewBookingUrl"));
        Assert.True(bookingDetailsProperties.ContainsKey("cancelBookingUrl"));
        Assert.True(bookingDetailsProperties.ContainsKey("rescheduleBookingUrl"));
    }

    [Fact]
    public void CreateOpenApiJson_SprintThreeApprovalRoutesIncludeTypedContractsAndExamples()
    {
        var json = BookingOpenApiDocumentFactory.CreateOpenApiJson(new Uri("https://localhost/api/openapi/v1.json"));
        var document = JsonNode.Parse(json)!.AsObject();
        var paths = document["paths"]!.AsObject();
        var schemas = document["components"]!["schemas"]!.AsObject();

        var createPost = paths["/v1/bookings/{bookingId}/approval-requests"]!["post"]!.AsObject();
        var adviserListGet = paths["/v1/adviser/booking-change-requests"]!["get"]!.AsObject();
        var pendingGet = paths["/v1/approval-requests/pending"]!["get"]!.AsObject();
        var reviewPost = paths["/v1/approval-requests/{requestId}/review"]!["post"]!.AsObject();

        Assert.Equal("Approvals", createPost["tags"]![0]!.GetValue<string>());
        Assert.Equal("Create adviser approval request", createPost["summary"]!.GetValue<string>());
        Assert.Contains("authenticated domain user", createPost["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ignored for security", createPost["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("#/components/schemas/CreateApprovalRequest", createPost["requestBody"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>());
        Assert.Equal("#/components/schemas/ApiResponseOfApprovalRequestResponse", createPost["responses"]!["201"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>());
        Assert.Equal("Rearrange", createPost["requestBody"]!["content"]!["application/json"]!["example"]!["changeType"]!.GetValue<string>());

        var createSchemaProperties = schemas["CreateApprovalRequest"]!["properties"]!.AsObject();
        Assert.True(createSchemaProperties.ContainsKey("adviserNote"));
        Assert.True(createSchemaProperties.ContainsKey("proposedAlternativeTimes"));

        Assert.Equal("List adviser booking change requests", adviserListGet["summary"]!.GetValue<string>());
        Assert.Contains("authenticated adviser", adviserListGet["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(adviserListGet["parameters"]!.AsArray(), x => x!["name"]!.GetValue<string>() == "bookingId");
        Assert.Contains(adviserListGet["parameters"]!.AsArray(), x => x!["name"]!.GetValue<string>() == "status");
        Assert.Contains(adviserListGet["parameters"]!.AsArray(), x => x!["name"]!.GetValue<string>() == "changeType");
        Assert.Contains(adviserListGet["parameters"]!.AsArray(), x => x!["name"]!.GetValue<string>() == "page");
        Assert.Contains(adviserListGet["parameters"]!.AsArray(), x => x!["name"]!.GetValue<string>() == "pageSize");
        Assert.StartsWith("#/components/schemas/ApiResponseOf", adviserListGet["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>(), StringComparison.Ordinal);

        Assert.Equal("List pending approval requests", pendingGet["summary"]!.GetValue<string>());
        Assert.Contains(pendingGet["parameters"]!.AsArray(), x => x!["name"]!.GetValue<string>() == "page");
        Assert.Contains(pendingGet["parameters"]!.AsArray(), x => x!["name"]!.GetValue<string>() == "pageSize");
        Assert.Equal("Review approval request", reviewPost["summary"]!.GetValue<string>());
        Assert.Contains("shared booking lifecycle workflow", reviewPost["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("#/components/schemas/ReviewApprovalRequest", reviewPost["requestBody"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>());
        Assert.Equal("slot-456", reviewPost["requestBody"]!["content"]!["application/json"]!["example"]!["selectedSlotId"]!.GetValue<string>());
    }

    [Fact]
    public void CreateOpenApiJson_AdminBookingSearchDocumentsFiltersAndPagedResponse()
    {
        var json = BookingOpenApiDocumentFactory.CreateOpenApiJson(new Uri("https://localhost/api/openapi/v1.json"));
        var document = JsonNode.Parse(json)!.AsObject();
        var paths = document["paths"]!.AsObject();
        var schemas = document["components"]!["schemas"]!.AsObject();

        var searchGet = paths["/v1/admin/bookings"]!["get"]!.AsObject();
        var parameters = searchGet["parameters"]!.AsArray();

        Assert.Equal("Search admin bookings", searchGet["summary"]!.GetValue<string>());
        Assert.Equal("#/components/schemas/ApiResponseOfAdminBookingSearchResponse", searchGet["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>());
        Assert.Contains(parameters, x => x!["name"]!.GetValue<string>() == "bookingId");
        Assert.Contains(parameters, x => x!["name"]!.GetValue<string>() == "transactionId");
        Assert.Contains(parameters, x => x!["name"]!.GetValue<string>() == "transactionRef");
        Assert.Contains(parameters, x => x!["name"]!.GetValue<string>() == "status");
        Assert.Contains(parameters, x => x!["name"]!.GetValue<string>() == "adviserId");
        Assert.Contains(parameters, x => x!["name"]!.GetValue<string>() == "clientRef");
        Assert.Contains(parameters, x => x!["name"]!.GetValue<string>() == "from");
        Assert.Contains(parameters, x => x!["name"]!.GetValue<string>() == "to");
        Assert.Contains(parameters, x => x!["name"]!.GetValue<string>() == "page");
        Assert.Contains(parameters, x => x!["name"]!.GetValue<string>() == "pageSize");

        var searchSchemaProperties = schemas["AdminBookingSearchResponse"]!["properties"]!.AsObject();
        Assert.True(searchSchemaProperties.ContainsKey("items"));
        Assert.True(searchSchemaProperties.ContainsKey("page"));
        Assert.True(searchSchemaProperties.ContainsKey("pageSize"));
        Assert.True(searchSchemaProperties.ContainsKey("totalItems"));
        Assert.True(searchSchemaProperties.ContainsKey("totalPages"));
    }

    [Fact]
    public void CreateOpenApiJson_SprintFourAdminRoutesDocumentDirectActionsAndCalendarRestore()
    {
        var json = BookingOpenApiDocumentFactory.CreateOpenApiJson(new Uri("https://localhost/api/openapi/v1.json"));
        var document = JsonNode.Parse(json)!.AsObject();
        var paths = document["paths"]!.AsObject();
        var schemas = document["components"]!["schemas"]!.AsObject();

        var cancelPost = paths["/v1/bookings/{bookingId}/cancel"]!["post"]!.AsObject();
        var optionsPost = paths["/v1/bookings/{bookingId}/rearrangement/options"]!["post"]!.AsObject();
        var rearrangePost = paths["/v1/bookings/{bookingId}/rearrange"]!["post"]!.AsObject();
        var calendarPost = paths["/v1/bookings/{bookingId}/calendar/remediate-showas"]!["post"]!.AsObject();

        Assert.Contains("Manager/admin direct cancellation", cancelPost["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reasonCode is required", cancelPost["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("#/components/schemas/CancelBookingRequest", cancelPost["requestBody"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>());
        Assert.Equal("ManagerApprovedCancellation", cancelPost["requestBody"]!["content"]!["application/json"]!["example"]!["reasonCode"]!.GetValue<string>());

        Assert.Contains("availability transactionId", optionsPost["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("#/components/schemas/RearrangementOptionsRequest", optionsPost["requestBody"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>());

        Assert.Contains("current existing booking", rearrangePost["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("newSlotId and reasonCode are required", rearrangePost["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("#/components/schemas/RearrangeBookingRequest", rearrangePost["requestBody"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>());
        Assert.Equal("slot-456", rearrangePost["requestBody"]!["content"]!["application/json"]!["example"]!["newSlotId"]!.GetValue<string>());

        Assert.Equal("Internal/Admin", calendarPost["tags"]![0]!.GetValue<string>());
        Assert.Equal("Remediate booking calendar event", calendarPost["summary"]!.GetValue<string>());
        Assert.Contains("recreates the confirmed Busy calendar event", calendarPost["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not cancel the booking", calendarPost["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("#/components/schemas/ApiResponseOfCalendarShowAsRemediationResult", calendarPost["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>());

        var calendarProperties = schemas["CalendarShowAsRemediationResult"]!["properties"]!.AsObject();
        Assert.True(calendarProperties.ContainsKey("previousEventId"));
        Assert.True(calendarProperties.ContainsKey("restoredMissingEvent"));
    }
}
