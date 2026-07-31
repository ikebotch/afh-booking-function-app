using AFH.Booking.Function.Http;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Notifications.Admin;

[BookingOpenApiTag("Notifications")]
public sealed class NotificationStatusFunction
{
    private readonly INotificationStatusService _status;
    private readonly INotificationAdminOperationService _operations;

    public NotificationStatusFunction(
        INotificationStatusService status,
        INotificationAdminOperationService operations)
    {
        _status = status;
        _operations = operations;
    }

    [Function("Notifications_Requests_Get")]
    [BookingOpenApiOperation(
        "Notifications",
        "Get notification request status",
        ResponseType = typeof(NotificationRequestStatus))]
    public async Task<HttpResponseData> GetRequestAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/notifications/requests/{id:guid}")] HttpRequestData req,
        Guid id,
        CancellationToken ct)
    {
        var result = await _status.GetRequestAsync(id, ct);
        return result is null
            ? await req.ProblemAsync(HttpStatusCode.NotFound, "Notification request was not found.", ct, "NotFound")
            : await req.OkJsonAsync(result, ct);
    }

    [Function("Notifications_Requests_List")]
    [BookingOpenApiOperation(
        "Notifications",
        "List notification requests",
        ResponseType = typeof(IReadOnlyList<NotificationRequestSummary>))]
    [BookingOpenApiQueryParameter("sourceApplication", "string", Description = "Optional source application filter.", Example = "Booking")]
    [BookingOpenApiQueryParameter("sourceReferenceType", "string", Description = "Optional source reference type filter.", Example = "Booking")]
    [BookingOpenApiQueryParameter("sourceReferenceId", "string", Description = "Optional source reference id filter.", Example = "booking-123")]
    [BookingOpenApiQueryParameter("notificationType", "string", Description = "Optional notification type filter.", Example = "BookingConfirmed")]
    [BookingOpenApiQueryParameter("status", "string", Description = "Optional request/dispatch status filter.", Example = "Pending")]
    [BookingOpenApiQueryParameter("fromUtc", "string", Format = "date-time", Description = "Optional UTC lower bound.", Example = "2026-07-01T00:00:00Z")]
    [BookingOpenApiQueryParameter("toUtc", "string", Format = "date-time", Description = "Optional UTC upper bound.", Example = "2026-07-31T23:59:59Z")]
    public async Task<HttpResponseData> QueryRequestsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/notifications/requests")] HttpRequestData req,
        CancellationToken ct)
    {
        return await req.OkJsonAsync(await _status.QueryRequestsAsync(new NotificationRequestQuery(
            req.Query("sourceApplication"),
            req.Query("sourceReferenceType"),
            req.Query("sourceReferenceId"),
            req.Query("notificationType"),
            ParseStatus(req.Query("status")),
            ParseDate(req.Query("fromUtc")),
            ParseDate(req.Query("toUtc"))), ct), ct);
    }

    [Function("Notifications_Dispatches_Get")]
    [BookingOpenApiOperation(
        "Notifications",
        "Get notification dispatch",
        ResponseType = typeof(NotificationDispatchSummary))]
    public async Task<HttpResponseData> GetDispatchAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/notifications/dispatches/{id}")] HttpRequestData req,
        string id,
        CancellationToken ct)
    {
        var result = await _status.GetDispatchAsync(id, ct);
        return result is null
            ? await req.ProblemAsync(HttpStatusCode.NotFound, "Notification dispatch was not found.", ct, "NotFound")
            : await req.OkJsonAsync(result, ct);
    }

    [Function("Notifications_MessageLogs_Get")]
    [BookingOpenApiOperation(
        "Notifications",
        "Get notification message log",
        ResponseType = typeof(NotificationMessageLogDetail))]
    public async Task<HttpResponseData> GetMessageLogAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/notifications/message-logs/{id:guid}")] HttpRequestData req,
        Guid id,
        CancellationToken ct)
    {
        var result = await _status.GetMessageLogAsync(id, ct);
        return result is null
            ? await req.ProblemAsync(HttpStatusCode.NotFound, "Notification message log was not found.", ct, "NotFound")
            : await req.OkJsonAsync(result, ct);
    }

    [Function("Notifications_Requests_Requeue")]
    [BookingOpenApiOperation(
        "Notifications",
        "Requeue notification request",
        ResponseType = typeof(NotificationAdminOperationResult),
        SuccessStatusCode = HttpStatusCode.Accepted)]
    public Task<HttpResponseData> RequeueAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/notifications/requests/{id:guid}/requeue")] HttpRequestData req,
        Guid id,
        CancellationToken ct)
        => RunOperationAsync(req, () => _operations.RequeueAsync(id, ct), ct);

    [Function("Notifications_Requests_DeadLetter")]
    [BookingOpenApiOperation(
        "Notifications",
        "Dead-letter notification request",
        RequestBodyType = typeof(AdminReasonRequest),
        ResponseType = typeof(NotificationAdminOperationResult),
        SuccessStatusCode = HttpStatusCode.Accepted,
        RequestExampleJson = """
        {
          "reason": "Suppressing duplicate delivery after provider outage."
        }
        """)]
    public async Task<HttpResponseData> DeadLetterAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/notifications/requests/{id:guid}/dead-letter")] HttpRequestData req,
        Guid id,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<AdminReasonRequest>(ct) ?? new AdminReasonRequest(null);
        return await RunOperationAsync(req, () => _operations.DeadLetterAsync(id, body.Reason ?? "Admin dead-lettered", ct), ct);
    }

    [Function("Notifications_Requests_MarkFailed")]
    [BookingOpenApiOperation(
        "Notifications",
        "Mark notification request failed",
        RequestBodyType = typeof(AdminReasonRequest),
        ResponseType = typeof(NotificationAdminOperationResult),
        SuccessStatusCode = HttpStatusCode.Accepted,
        RequestExampleJson = """
        {
          "reason": "Provider rejected the payload."
        }
        """)]
    public async Task<HttpResponseData> MarkFailedAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/notifications/requests/{id:guid}/mark-failed")] HttpRequestData req,
        Guid id,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<AdminReasonRequest>(ct) ?? new AdminReasonRequest(null);
        return await RunOperationAsync(req, () => _operations.MarkFailedAsync(id, body.Reason ?? "Admin marked failed", ct), ct);
    }

    private static async Task<HttpResponseData> RunOperationAsync(
        HttpRequestData req,
        Func<Task<NotificationAdminOperationResult>> operation,
        CancellationToken ct)
    {
        try
        {
            return await req.AcceptedJsonAsync(await operation(), ct);
        }
        catch (NotificationRequestValidationException ex)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, ex.Message, ct, "Validation");
        }
    }

    private static NotificationDispatchStatus? ParseStatus(string? value)
        => Enum.TryParse<NotificationDispatchStatus>(value, ignoreCase: true, out var status)
            ? status
            : null;

    private static DateTime? ParseDate(string? value)
        => DateTime.TryParse(value, out var result) ? result.ToUniversalTime() : null;

    public sealed record AdminReasonRequest(string? Reason);
}
