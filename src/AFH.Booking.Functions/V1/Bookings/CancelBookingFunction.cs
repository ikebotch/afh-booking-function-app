using System.Net;
using System.Text.Json;
using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Functions.V1.Bookings;

public sealed class CancelBookingFunction
{
    private readonly IApprovalWorkflowService _approvals;
    private readonly ICancelBookingHandler _handler;
    private readonly ILogger<CancelBookingFunction> _logger;

    public CancelBookingFunction(
        IApprovalWorkflowService approvals,
        ICancelBookingHandler handler,
        ILogger<CancelBookingFunction> logger)
    {
        _approvals = approvals;
        _handler = handler;
        _logger = logger;
    }

    [Function("Bookings_CancelBooking")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/bookings/{bookingId}/cancel")]
        HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(bookingId))
                return await req.ProblemAsync(HttpStatusCode.BadRequest, "bookingId is required.", ct, "Validation");

            var body = await req.ReadJsonAsync<CancelBookingRequest>(ct);
            var requestedBy = string.IsNullOrWhiteSpace(body?.RequestedBy) ? "Client" : body!.RequestedBy!.Trim();

            if (string.Equals(requestedBy, "Adviser", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(body?.ApprovalRequestId))
                {
                    return await req.ProblemAsync(
                        HttpStatusCode.Forbidden,
                        "Adviser cancellation requires an approved approvalRequestId.",
                        ct,
                        "ApprovalRequired");
                }

                var approved = await _approvals.IsApprovedAsync(
                    body.ApprovalRequestId.Trim(),
                    bookingId.Trim(),
                    changeType: "Cancel",
                    requestedBy: "Adviser",
                    ct: ct);

                if (!approved)
                {
                    return await req.ProblemAsync(
                        HttpStatusCode.Forbidden,
                        "Approval request is not approved for this booking cancellation.",
                        ct,
                        "ApprovalRequired");
                }
            }

            var cmd = new CancelBookingCommand
            {
                BookingId = bookingId.Trim(),
                Reason = BuildReason(body)
            };

            var result = await _handler.HandleAsync(cmd, ct);

            if (!result.IsSuccess)
                return await req.ProblemAsync(
                    result.StatusCode,
                    result.ErrorMessage ?? "Request failed.",
                    ct,
                    result.ErrorCode);

            // If your handler returns a payload (recommended)
            if (result.Value is not null)
                return await req.OkJsonAsync(result.Value, ct);

            // If handler returns success without data
            return await req.OkJsonAsync(new { message = "Booking cancelled." }, ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON body in Bookings_CancelBooking.");
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Invalid JSON body.", ct, "InvalidJson");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Bookings_CancelBooking.");
            return await req.ProblemAsync(HttpStatusCode.InternalServerError, "Something went wrong.", ct, "ServerError");
        }
    }

    private static string BuildReason(CancelBookingRequest? request)
    {
        if (request is null)
            return "Cancelled";

        if (!string.IsNullOrWhiteSpace(request.Reason))
            return request.Reason.Trim();

        var reasonCode = string.IsNullOrWhiteSpace(request.ReasonCode)
            ? "Unspecified"
            : request.ReasonCode.Trim();

        var requestedBy = string.IsNullOrWhiteSpace(request.RequestedBy)
            ? "Unknown"
            : request.RequestedBy.Trim();

        var detail = string.IsNullOrWhiteSpace(request.ReasonDetail)
            ? string.Empty
            : $": {request.ReasonDetail.Trim()}";

        return $"{requestedBy} - {reasonCode}{detail}";
    }
}
