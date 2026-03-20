using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Functions.V1.Bookings;

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

        var created = await _approvals.CreateAsync(
            bookingId: bookingId.Trim(),
            changeType: changeType,
            requestedBy: "Adviser",
            reasonCode: body?.ReasonCode,
            reasonDetail: body?.ReasonDetail,
            ct: ct);

        return await req.CreatedJsonAsync(created, ct);
    }

    private static bool IsAllowedChangeType(string value)
        => string.Equals(value, "Cancel", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "Rearrange", StringComparison.OrdinalIgnoreCase);
}
