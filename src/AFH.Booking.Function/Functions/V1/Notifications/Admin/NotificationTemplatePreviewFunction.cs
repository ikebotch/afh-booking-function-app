using AFH.Booking.Function.Http;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Notifications.Admin;

[BookingOpenApiTag("Notifications")]
public sealed class NotificationTemplatePreviewFunction
{
    private readonly INotificationTemplatePreviewService _preview;

    public NotificationTemplatePreviewFunction(INotificationTemplatePreviewService preview)
    {
        _preview = preview;
    }

    [Function("Notifications_Templates_Preview")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/notifications/templates/preview")] HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<PreviewRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, "Validation");

        try
        {
            return await req.OkJsonAsync(await _preview.PreviewAsync(body.ToModel(), ct), ct);
        }
        catch (NotificationRequestValidationException ex)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, ex.Message, ct, "Validation");
        }
    }

    public sealed record PreviewRequest(
        string TemplateKey,
        string TemplateVersion,
        NotificationChannel Channel,
        string? SubjectTemplate,
        string? BodyTemplate,
        string ContentType,
        IReadOnlyDictionary<string, string>? Data)
    {
        public NotificationTemplatePreviewRequest ToModel()
            => new(TemplateKey, TemplateVersion, Channel, SubjectTemplate, BodyTemplate, ContentType, Data ?? new Dictionary<string, string>());
    }
}
