using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Auth;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Security.Claims;

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

        var requestedBy = string.IsNullOrWhiteSpace(body?.RequestedBy) ? "Adviser" : body.RequestedBy.Trim();
        if (!string.Equals(requestedBy, "Adviser", StringComparison.OrdinalIgnoreCase))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Only Adviser requests require approval.", ct, "Validation");

        if (string.IsNullOrWhiteSpace(body?.ReasonCode))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "reasonCode is required for adviser approval requests.", ct, "Validation");

        if (string.Equals(changeType, "Rearrange", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(body?.NewSlotId) &&
            body?.ProposedAlternativeTimes.Any(x => !string.IsNullOrWhiteSpace(x.SlotId)) != true)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "newSlotId or proposedAlternativeTimes is required for adviser rearrangement approval requests.", ct, "Validation");
        }

        var actor = BuildAdviserActorContext(context, BookingChangeRequestContext.GetCorrelationId(req));

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

        return await req.CreatedJsonAsync(created.ToContract(), ct);
    }

    private static bool IsAllowedChangeType(string value)
        => string.Equals(value, "Cancel", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "Rearrange", StringComparison.OrdinalIgnoreCase);

    private static BookingActorContext BuildAdviserActorContext(FunctionContext context, string? correlationId)
    {
        var user = context.GetDomainUserContext();
        var principal = context.GetDomainUserPrincipal();
        var actorId = user?.UserId ?? GetClaimValue(principal, "oid", "http://schemas.microsoft.com/identity/claims/objectidentifier", ClaimTypes.NameIdentifier) ?? user?.Email ?? GetClaimValue(principal, ClaimTypes.Email, "email", ClaimTypes.Upn, "preferred_username");
        var displayName = user?.DisplayName ?? GetClaimValue(principal, "name", ClaimTypes.Name);

        return BookingActorContext.AdviserPortal(
            actorId,
            displayName,
            correlationId,
            user?.Permissions);
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
