using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Functions.V1.Bookings;

public sealed class RearrangeBookingFunction
{
    private readonly IRearrangeBookingHandler _handler;
    private readonly IApprovalWorkflowService _approvals;

    public RearrangeBookingFunction(
        IRearrangeBookingHandler handler,
        IApprovalWorkflowService approvals)
    {
        _handler = handler;
        _approvals = approvals;
    }

    [Function("Bookings_Rearrange")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/bookings/{bookingId}/rearrange")]
        HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "bookingId is required.", ct, "Validation");

        var body = await req.ReadJsonAsync<RearrangeBookingRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, "Validation");

        var requestedBy = string.IsNullOrWhiteSpace(body.RequestedBy) ? "Client" : body.RequestedBy.Trim();

        if (string.Equals(requestedBy, "Adviser", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(body.ApprovalRequestId))
            {
                return await req.ProblemAsync(
                    HttpStatusCode.Forbidden,
                    "Adviser rearrangement requires an approved approvalRequestId.",
                    ct,
                    "ApprovalRequired");
            }

            var approved = await _approvals.IsApprovedAsync(
                body.ApprovalRequestId.Trim(),
                bookingId.Trim(),
                changeType: "Rearrange",
                requestedBy: "Adviser",
                ct: ct);

            if (!approved)
            {
                return await req.ProblemAsync(
                    HttpStatusCode.Forbidden,
                    "Approval request is not approved for this booking rearrangement.",
                    ct,
                    "ApprovalRequired");
            }
        }

        var cmd = new RearrangeBookingCommand
        {
            BookingId = bookingId.Trim(),
            NewSlotId = body.NewSlotId,
            RequestedBy = requestedBy,
            ReasonCode = body.ReasonCode,
            ReasonDetail = body.ReasonDetail
        };

        var result = await _handler.HandleAsync(cmd, ct);

        if (!result.IsSuccess)
        {
            return await req.ProblemAsync(
                result.StatusCode,
                result.ErrorMessage ?? "Request failed.",
                ct,
                result.ErrorCode);
        }

        return await req.OkJsonAsync(result.Value!, ct);
    }
}
