using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Common;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Function.Http;
using AFH.Booking.Function.Mapping;

namespace AFH.Booking.Function.Functions.V1.Availability;

[BookingOpenApiTag("Availability")]
public sealed class GetAvailabilityFunction
{
    private readonly IAvailabilityService _service;
    private readonly ILogger<GetAvailabilityFunction> _logger;

    public GetAvailabilityFunction(
        IAvailabilityService service,
        ILogger<GetAvailabilityFunction> logger)
    {
        _service = service;
        _logger = logger;
    }


 


    [Function("Transactions_Availability")]
    [BookingOpenApiOperation(
        "Availability",
        "Get availability",
        Description = "Returns adviser availability for the transaction. Sprint 5 availability governance is applied for both remote and in-person bookings: only active/skilled advisers are considered, preferred adviser ids do not create synthetic adviser profiles, working pattern, minimum duration and adviser capacity rules are enforced, and selected slots are revalidated again during hold creation. Available slots include `scoreBreakdown` rule audit keys such as `rule.workingPatternAllowed`, `rule.capacityAllowed`, and `rule.minimumDurationAllowed`. In-person requests should include `destinationAddress` so travel and proximity checks can be evaluated.",
        RequestBodyType = typeof(GetAvailabilityRequest),
        ResponseType = typeof(GetAvailabilityResponse),
        RequestExampleJson = """
                             {
                               "clientId": "client-123",
                               "preferredStartUtc": "2026-06-20T10:00:00Z",
                               "duration": 60,
                               "isRemote": false,
                               "meetingType": "Review",
                               "destinationAddress": {
                                 "line1": "42 King Street",
                                 "town": "Manchester",
                                 "postcode": "M2 4LQ",
                                 "country": "UK"
                               },
                               "preferredAdviserIds": [ "adviser-123" ],
                               "regions": [ "North West" ],
                               "requiredSkills": [ "Pensions" ],
                               "excludeAdviserIds": [],
                               "searchHorizonMinutes": 180,
                               "maxCandidates": 100,
                               "limit": 10
                             }
                             """,
        ResponseExampleJson = """
                              {
                                "data": {
                                  "transactionId": "transaction-123",
                                  "advisers": [
                                    {
                                      "id": "adviser-123",
                                      "name": "Alex Adviser",
                                      "goldStar": true,
                                      "slots": [
                                        {
                                          "slotId": "slot-123",
                                          "startUtc": "2026-06-20T10:00:00Z",
                                          "endUtc": "2026-06-20T11:00:00Z",
                                          "rating": 96,
                                          "scoreBreakdown": {
                                            "rule.workingPatternAllowed": 1,
                                            "rule.capacityAllowed": 1,
                                            "rule.minimumDurationAllowed": 1
                                          },
                                          "travelMinutes": 8,
                                          "companyBufferMinutes": 15,
                                          "distanceMiles": 1.2,
                                          "travelStatus": "Ok"
                                        }
                                      ]
                                    }
                                  ],
                                  "paging": {
                                    "items": [],
                                    "total": 1,
                                    "pageSize": 10,
                                    "nextCursor": null
                                  }
                                }
                              }
                              """)]
    public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/transactions/{transactionId}/availability")]
        HttpRequestData req,
            string transactionId,
            CancellationToken ct)
    {

   
        if (string.IsNullOrWhiteSpace(transactionId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "transactionId is required.", ct);

        var body = await req.ReadJsonAsync<GetAvailabilityRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct);

        if (body.Duration <= 0)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "durationMinutes must be > 0.", ct);

        // preferredStartUtc can be date-only or date-time
        if (!AvailabilityParsing.TryParsePreferredStart(body.PreferredStartUtc, out var preferred))
            return await req.ProblemAsync(HttpStatusCode.BadRequest,
                "preferredStartUtc must be either 'yyyy-MM-dd' or ISO-8601 UTC e.g. '2026-02-01T10:00:00Z'.", ct);

        // window optional, but if provided must be valid
        if (body.Window is not null)
        {
            var ws = DateTime.SpecifyKind(body.Window.StartUtc, DateTimeKind.Utc);
            var we = DateTime.SpecifyKind(body.Window.EndUtc, DateTimeKind.Utc);

            if (ws == default || we == default || we <= ws)
                return await req.ProblemAsync(HttpStatusCode.BadRequest, "window.startUtc and window.endUtc must be valid and endUtc > startUtc.", ct);
        }


        var query = body.ToQuery(transactionId);

        var result = await _service.HandleAsync(query, ct);

        if (!result.IsSuccess)
            return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

        var payload = result.Value!.ToContract();
        return await req.OkJsonAsync(payload, ct);
    }


}
