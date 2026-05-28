using AFH.Booking.Function.Http;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Notifications.Admin;

[BookingOpenApiTag("Notifications")]
public sealed class NotificationTemplatesFunction
{
    private readonly INotificationTemplateAdminService _templates;

    public NotificationTemplatesFunction(INotificationTemplateAdminService templates)
    {
        _templates = templates;
    }

    [Function("Notifications_Templates_List")]
    public async Task<HttpResponseData> ListAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/notifications/templates")] HttpRequestData req,
        CancellationToken ct)
    {
        var query = new NotificationTemplateQuery(
            req.Query("templateKey"),
            ParseChannel(req.Query("channel")),
            ParseBool(req.Query("isActive")));

        return await req.OkJsonAsync(await _templates.ListAsync(query, ct), ct);
    }

    [Function("Notifications_Templates_Get")]
    public async Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/notifications/templates/{id:guid}")] HttpRequestData req,
        Guid id,
        CancellationToken ct)
    {
        var template = await _templates.GetAsync(id, ct);
        return template is null
            ? await req.ProblemAsync(HttpStatusCode.NotFound, "Template was not found.", ct, "NotFound")
            : await req.OkJsonAsync(template, ct);
    }

    [Function("Notifications_Templates_GetByKey")]
    public async Task<HttpResponseData> GetByKeyAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/notifications/templates/by-key/{templateKey}/versions/{templateVersion}/channels/{channel}")] HttpRequestData req,
        string templateKey,
        string templateVersion,
        string channel,
        CancellationToken ct)
    {
        if (!Enum.TryParse<NotificationChannel>(channel, ignoreCase: true, out var parsedChannel) || parsedChannel == NotificationChannel.Unknown)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Channel is invalid.", ct, "Validation");

        var template = await _templates.GetAsync(templateKey, templateVersion, parsedChannel, ct);
        return template is null
            ? await req.ProblemAsync(HttpStatusCode.NotFound, "Template was not found.", ct, "NotFound")
            : await req.OkJsonAsync(template, ct);
    }

    [Function("Notifications_Templates_Create")]
    public async Task<HttpResponseData> CreateAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/notifications/templates")] HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<TemplateUpsertRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, "Validation");

        try
        {
            return await req.CreatedJsonAsync(await _templates.CreateAsync(body.ToModel(GetActor(req)), ct), ct);
        }
        catch (NotificationRequestValidationException ex)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, ex.Message, ct, "Validation");
        }
    }

    [Function("Notifications_Templates_Update")]
    public async Task<HttpResponseData> UpdateAsync(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "v1/notifications/templates/{id:guid}")] HttpRequestData req,
        Guid id,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<TemplateUpsertRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, "Validation");

        try
        {
            return await req.OkJsonAsync(await _templates.UpdateAsync(id, body.ToModel(GetActor(req)), ct), ct);
        }
        catch (NotificationRequestValidationException ex)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, ex.Message, ct, "Validation");
        }
    }

    [Function("Notifications_Templates_Activate")]
    public Task<HttpResponseData> ActivateAsync(
        [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "v1/notifications/templates/{id:guid}/activate")] HttpRequestData req,
        Guid id,
        CancellationToken ct)
        => SetActiveAsync(req, id, true, ct);

    [Function("Notifications_Templates_Deactivate")]
    public Task<HttpResponseData> DeactivateAsync(
        [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "v1/notifications/templates/{id:guid}/deactivate")] HttpRequestData req,
        Guid id,
        CancellationToken ct)
        => SetActiveAsync(req, id, false, ct);

    private async Task<HttpResponseData> SetActiveAsync(HttpRequestData req, Guid id, bool isActive, CancellationToken ct)
    {
        try
        {
            return await req.OkJsonAsync(await _templates.SetActiveAsync(id, isActive, GetActor(req), ct), ct);
        }
        catch (NotificationRequestValidationException ex)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, ex.Message, ct, "Validation");
        }
    }

    private static NotificationChannel? ParseChannel(string? value)
        => Enum.TryParse<NotificationChannel>(value, ignoreCase: true, out var channel) && channel != NotificationChannel.Unknown
            ? channel
            : null;

    private static bool? ParseBool(string? value)
        => bool.TryParse(value, out var result) ? result : null;

    private static string? GetActor(HttpRequestData req)
        => req.Headers.TryGetValues("x-user", out var values)
            ? values.FirstOrDefault()
            : null;

    public sealed record TemplateUpsertRequest(
        string TemplateKey,
        string TemplateVersion,
        NotificationChannel Channel,
        string Name,
        string? Description,
        string? SubjectTemplate,
        string BodyTemplate,
        string ContentType,
        bool IsActive)
    {
        public NotificationTemplateUpsert ToModel(string? actor)
            => new(TemplateKey, TemplateVersion, Channel, Name, Description, SubjectTemplate, BodyTemplate, ContentType, IsActive, actor);
    }
}
