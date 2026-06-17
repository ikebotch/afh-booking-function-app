using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Application.Models.Auth;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Domain.Auth;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Auth;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Security.Claims;
using ContractApprovalRequestResponse = AFH.Booking.Contracts.V1.Responses.ApprovalRequestResponse;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Approvals")]
public sealed class CreateApprovalRequestFunction
{
    private readonly IApprovalWorkflowService _approvals;

    public CreateApprovalRequestFunction(IApprovalWorkflowService approvals)
    {
        _approvals = approvals;
    }

    [Function("Bookings_CreateApprovalRequest")]
    [BookingOpenApiOperation(
        "Approvals",
        "Create adviser approval request",
        Description = "Creates an adviser booking change approval request for the current booking. The route bookingId is the existing booking. The authenticated domain user is used as the adviser/requester; request body actor fields are ignored for security. changeType must be Cancel or Rearrange. reasonCode is required. Rearrangement requests require either newSlotId or proposedAlternativeTimes.",
        RequestBodyType = typeof(CreateApprovalRequest),
        ResponseType = typeof(ContractApprovalRequestResponse),
        SuccessStatusCode = HttpStatusCode.Created,
        RequestExampleJson = """
        {
          "changeType": "Rearrange",
          "reasonCode": "AdviserUnavailable",
          "reasonDetail": "Adviser cannot attend the current slot.",
          "newSlotId": "slot-123",
          "adviserNote": "Preferred afternoon slot.",
          "proposedAlternativeTimes": [
            {
              "slotId": "slot-456",
              "adviserId": "adv-123",
              "startUtc": "2026-06-20T10:00:00Z",
              "endUtc": "2026-06-20T11:00:00Z",
              "note": "Best alternative",
              "preferenceOrder": 1
            }
          ]
        }
        """)]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/bookings/{bookingId}/approval-requests")]
        HttpRequestData req,
        FunctionContext context,
        string bookingId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "bookingId is required.", ct, "Validation");

        var body = await req.ReadJsonAsync<CreateApprovalRequest>(ct);

        var changeType = string.IsNullOrWhiteSpace(body?.ChangeType) ? "Rearrange" : body.ChangeType.Trim();
        if (!IsAllowedChangeType(changeType))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "changeType must be 'Cancel' or 'Rearrange'.", ct, "Validation");

        var user = context.GetDomainUserContext();
        if (user is null)
            return await req.ProblemAsync(HttpStatusCode.Unauthorized, "Authenticated adviser identity is required.", ct, "Unauthorized");

        if (!user.Permissions.Contains(BookingPermissionNames.ApprovalRequestsCreate, StringComparer.OrdinalIgnoreCase))
        {
            return await req.ProblemAsync(
                HttpStatusCode.Forbidden,
                $"Permission '{BookingPermissionNames.ApprovalRequestsCreate}' is required.",
                ct,
                "Forbidden");
        }

        if (string.IsNullOrWhiteSpace(user.AdviserId))
            return await req.ProblemAsync(HttpStatusCode.Forbidden, "Signed-in user is not mapped to an adviser profile.", ct, "Forbidden");

        if (string.IsNullOrWhiteSpace(body?.ReasonCode))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "reasonCode is required for adviser approval requests.", ct, "Validation");

        if (string.Equals(changeType, "Rearrange", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(body?.NewSlotId) &&
            body?.ProposedAlternativeTimes.Any(x => !string.IsNullOrWhiteSpace(x.SlotId)) != true)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "newSlotId or proposedAlternativeTimes is required for adviser rearrangement approval requests.", ct, "Validation");
        }

        var actor = BuildAdviserActorContext(context, user, BookingChangeRequestContext.GetCorrelationId(req));

        ApprovalRequestResponse created;
        try
        {
            created = await _approvals.CreateAsync(new CreateApprovalWorkflowRequest(
                BookingId: bookingId.Trim(),
                ChangeType: changeType,
                RequestedBy: "Adviser",
                RequesterId: actor.ActorId,
                ReasonCode: body?.ReasonCode,
                ReasonDetail: body?.ReasonDetail,
                NewSlotId: body?.NewSlotId,
                CorrelationId: actor.CorrelationId,
                ActorContext: actor,
                AdviserNote: body?.AdviserNote,
                ProposedAlternativeTimes: body?.ProposedAlternativeTimes.Select(ToApplication).ToList()), ct);
        }
        catch (InvalidOperationException ex)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, ex.Message, ct, "Validation");
        }
        catch (UnauthorizedAccessException ex)
        {
            return await req.ProblemAsync(HttpStatusCode.Forbidden, ex.Message, ct, "Forbidden");
        }

        return await req.CreatedJsonAsync(created.ToContract(), ct);
    }

    private static bool IsAllowedChangeType(string value)
        => string.Equals(value, "Cancel", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "Rearrange", StringComparison.OrdinalIgnoreCase);

    private static BookingActorContext BuildAdviserActorContext(
        FunctionContext context,
        AdviserUserContext user,
        string? correlationId)
    {
        var principal = context.GetDomainUserPrincipal();
        var actorId = user.AdviserId
            ?? user.UserId
            ?? GetClaimValue(principal, "oid", "http://schemas.microsoft.com/identity/claims/objectidentifier", ClaimTypes.NameIdentifier)
            ?? user.Email
            ?? GetClaimValue(principal, ClaimTypes.Email, "email", ClaimTypes.Upn, "preferred_username");
        var displayName = user.DisplayName ?? GetClaimValue(principal, "name", ClaimTypes.Name);

        return BookingActorContext.AdviserPortal(
            actorId,
            displayName,
            correlationId,
            user.Permissions);
    }

    private static ApprovalProposedAlternativeTime ToApplication(ApprovalProposedAlternativeTimeRequest request)
        => new()
        {
            SlotId = request.SlotId,
            AdviserId = request.AdviserId,
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            Note = request.Note,
            PreferenceOrder = request.PreferenceOrder
        };

    private static string? GetClaimValue(ClaimsPrincipal? principal, params string[] claimTypes)
    {
        if (principal is null)
            return null;

        foreach (var claimType in claimTypes)
        {
            var claim = principal.FindFirst(claimType);
            if (!string.IsNullOrWhiteSpace(claim?.Value))
                return claim.Value;
        }

        return null;
    }
}
