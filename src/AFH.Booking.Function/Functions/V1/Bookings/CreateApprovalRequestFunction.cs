using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

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
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/bookings/{bookingId}/approval-requests")]
        HttpRequestData req,
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

        if (string.Equals(changeType, "Rearrange", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(body?.NewSlotId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "newSlotId is required for adviser rearrangement approval requests.", ct, "Validation");

        ApprovalRequestResponse created;
        try
        {
            created = await _approvals.CreateAsync(new CreateApprovalWorkflowRequest(
                BookingId: bookingId.Trim(),
                ChangeType: changeType,
                RequestedBy: "Adviser",
                RequesterId: body?.RequesterId,
                ReasonCode: body?.ReasonCode,
                ReasonDetail: body?.ReasonDetail,
                NewSlotId: body?.NewSlotId,
                CorrelationId: BookingChangeRequestContext.GetCorrelationId(req)), ct);
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
}
