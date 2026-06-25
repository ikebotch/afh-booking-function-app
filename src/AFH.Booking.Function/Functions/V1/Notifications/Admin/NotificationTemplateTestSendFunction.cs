using System.Net;
using AFH.Booking.Function.Http;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Notifications.Admin;

[BookingOpenApiTag("Notifications")]
public sealed class NotificationTemplateTestSendFunction
{
    private readonly INotificationTemplateAdminService _templates;
    private readonly INotificationRequestIngestionService _ingestion;

    public NotificationTemplateTestSendFunction(
        INotificationTemplateAdminService templates,
        INotificationRequestIngestionService ingestion)
    {
        _templates = templates;
        _ingestion = ingestion;
    }

    [Function("Notifications_Templates_TestSend")]
    public async Task<HttpResponseData> TestSendAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/notifications/templates/{id:guid}/test-send")] HttpRequestData req,
        Guid id,
        CancellationToken ct)
    {
        var template = await _templates.GetAsync(id, ct);
        if (template is null)
            return await req.ProblemAsync(HttpStatusCode.NotFound, "Template was not found.", ct, "NotFound");

        var body = await req.ReadJsonAsync<TestSendRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, "Validation");

        if (body.Recipient is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "recipient is required.", ct, "Validation");

        var data = body.Data is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(body.Data, StringComparer.OrdinalIgnoreCase);

        data["TemplateKey"] = template.TemplateKey;
        data["TemplateVersion"] = template.TemplateVersion;
        data["TemplateChannel"] = template.Channel.ToString();
        data["TemplateTestSend"] = "true";

        var actor = new NotificationActor(
            "AdminUser",
            "AFH.Booking.Admin",
            null,
            GetActor(req) ?? body.ActorName,
            body.ActorEmail);

        var request = new NotificationRequested(
            new NotificationType("AFH.Booking.Admin", "TemplateTestSend"),
            string.IsNullOrWhiteSpace(body.CorrelationId) ? $"template-test-{id:N}-{DateTime.UtcNow:yyyyMMddHHmmss}" : body.CorrelationId!,
            actor,
            [body.Recipient.ToRecipient(template.Channel)],
            data);

        var accepted = await _ingestion.AcceptAsync(request, ct);
        return await req.OkJsonAsync(new
        {
            template.Id,
            template.TemplateKey,
            template.TemplateVersion,
            template.Channel,
            accepted.NotificationRequestId,
            accepted.Status,
            accepted.CorrelationId,
            accepted.Created
        }, ct);
    }

    private static string? GetActor(HttpRequestData req)
        => req.Headers.TryGetValues("x-user", out var values)
            ? values.FirstOrDefault()
            : null;

    public sealed record TestSendRequest(
        TestSendRecipient? Recipient,
        IReadOnlyDictionary<string, string>? Data,
        string? CorrelationId,
        string? ActorName,
        string? ActorEmail);

    public sealed record TestSendRecipient(
        string? RecipientType,
        string? DisplayName,
        string? Email,
        string? MobileNumber,
        string? PushTarget)
    {
        public NotificationRecipient ToRecipient(NotificationChannel channel)
            => new(
                string.IsNullOrWhiteSpace(RecipientType) ? "TestRecipient" : RecipientType!,
                DisplayName,
                Email,
                MobileNumber,
                PushTarget,
                [channel]);
    }
}
