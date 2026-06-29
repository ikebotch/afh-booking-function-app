using System.Net;
using AFH.Booking.Function.Http;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Notifications.Admin;

[BookingOpenApiTag("Notifications")]
public sealed class NotificationSettingsFunction
{
    private readonly INotificationSettingsService _settings;

    public NotificationSettingsFunction(INotificationSettingsService settings)
    {
        _settings = settings;
    }

    [Function("Notifications_Settings_List")]
    public async Task<HttpResponseData> ListAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/notifications/settings")] HttpRequestData req,
        CancellationToken ct)
        => await req.OkJsonAsync(await _settings.ListAsync(req.Query("category"), ct), ct);

    [Function("Notifications_Settings_Get")]
    public async Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/notifications/settings/{key}")] HttpRequestData req,
        string key,
        CancellationToken ct)
    {
        var setting = await _settings.GetAsync(key, ct);
        return setting is null
            ? await req.ProblemAsync(HttpStatusCode.NotFound, "Notification setting was not found.", ct, "NotFound")
            : await req.OkJsonAsync(setting, ct);
    }

    [Function("Notifications_Settings_Upsert")]
    public async Task<HttpResponseData> UpsertAsync(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "v1/notifications/settings/{key}")] HttpRequestData req,
        string key,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<SettingUpsertRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, "Validation");

        try
        {
            var result = await _settings.UpsertAsync(
                new NotificationSettingUpsert(
                    key,
                    body.Category ?? "General",
                    body.Value ?? string.Empty,
                    body.IsSecret ?? false,
                    body.Description,
                    GetActor(req)),
                ct);

            return await req.OkJsonAsync(result, ct);
        }
        catch (NotificationRequestValidationException ex)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, ex.Message, ct, "Validation");
        }
    }

    [Function("Notifications_Settings_Delete")]
    public async Task<HttpResponseData> DeleteAsync(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "v1/notifications/settings/{key}")] HttpRequestData req,
        string key,
        CancellationToken ct)
    {
        var deleted = await _settings.DeleteAsync(key, ct);
        return deleted
            ? await req.OkJsonAsync(new { key, deleted = true }, ct)
            : await req.ProblemAsync(HttpStatusCode.NotFound, "Notification setting was not found.", ct, "NotFound");
    }

    [Function("Notifications_ChannelSettings_List")]
    public async Task<HttpResponseData> ListChannelSettingsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/notifications/channel-settings")] HttpRequestData req,
        CancellationToken ct)
        => await req.OkJsonAsync(await _settings.ListChannelSettingsAsync(ct), ct);

    [Function("Notifications_ChannelSettings_Create")]
    public async Task<HttpResponseData> CreateChannelSettingAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/notifications/channel-settings")] HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<ChannelSettingUpsertRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, "Validation");

        try
        {
            return await req.OkJsonAsync(await _settings.UpsertChannelSettingAsync(body.ToModel(GetActor(req)), ct), ct);
        }
        catch (NotificationRequestValidationException ex)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, ex.Message, ct, "Validation");
        }
    }

    [Function("Notifications_ChannelSettings_Update")]
    public async Task<HttpResponseData> UpdateChannelSettingAsync(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "v1/notifications/channel-settings/{id}")] HttpRequestData req,
        string id,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<ChannelSettingUpsertRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, "Validation");

        try
        {
            return await req.OkJsonAsync(await _settings.UpsertChannelSettingAsync(body.ToModel(GetActor(req), id), ct), ct);
        }
        catch (NotificationRequestValidationException ex)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, ex.Message, ct, "Validation");
        }
    }

    [Function("Notifications_LifecycleEvents_List")]
    public async Task<HttpResponseData> ListLifecycleEventsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/notifications/lifecycle-events")] HttpRequestData req,
        CancellationToken ct)
        => await req.OkJsonAsync(await _settings.ListLifecycleEventsAsync(ct), ct);

    [Function("Notifications_RetryPolicies_List")]
    public async Task<HttpResponseData> ListRetryPoliciesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/notifications/retry-policies")] HttpRequestData req,
        CancellationToken ct)
        => await req.OkJsonAsync(await _settings.ListRetryPoliciesAsync(ct), ct);

    [Function("Notifications_RetryPolicies_Create")]
    public async Task<HttpResponseData> CreateRetryPolicyAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/notifications/retry-policies")] HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<RetryPolicyUpsertRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, "Validation");

        try
        {
            return await req.OkJsonAsync(await _settings.UpsertRetryPolicyAsync(body.ToModel(GetActor(req)), ct), ct);
        }
        catch (NotificationRequestValidationException ex)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, ex.Message, ct, "Validation");
        }
    }

    [Function("Notifications_RetryPolicies_Update")]
    public async Task<HttpResponseData> UpdateRetryPolicyAsync(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "v1/notifications/retry-policies/{id}")] HttpRequestData req,
        string id,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<RetryPolicyUpsertRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, "Validation");

        try
        {
            return await req.OkJsonAsync(await _settings.UpsertRetryPolicyAsync(body.ToModel(GetActor(req), id), ct), ct);
        }
        catch (NotificationRequestValidationException ex)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, ex.Message, ct, "Validation");
        }
    }

    private static string? GetActor(HttpRequestData req)
        => req.Headers.TryGetValues("x-user", out var values)
            ? values.FirstOrDefault()
            : null;

    public sealed record SettingUpsertRequest(string? Category, string? Value, bool? IsSecret, string? Description);

    public sealed record ChannelSettingUpsertRequest(
        string? Id,
        string? Channel,
        string? Provider,
        string? SenderId,
        string? Format,
        string? Status,
        string? Description)
    {
        public NotificationChannelSettingUpsert ToModel(string? actor, string? id = null)
            => new(id ?? Id, Channel, Provider, SenderId, Format, Status, Description, actor);
    }

    public sealed record RetryPolicyUpsertRequest(
        string? Id,
        string? EventType,
        string? Channel,
        int? MaxRetries,
        int? DelayMin,
        string? Strategy,
        string? Status,
        string? Description)
    {
        public NotificationRetryPolicyUpsert ToModel(string? actor, string? id = null)
            => new(id ?? Id, EventType, Channel, MaxRetries, DelayMin, Strategy, Status, Description, actor);
    }
}
