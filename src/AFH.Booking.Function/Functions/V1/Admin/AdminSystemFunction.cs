using System.Net;
using AFH.Booking.Function.Http;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Function.Functions.V1.Admin;

[BookingOpenApiTag("Admin")]
public sealed class AdminSystemFunction
{
    private readonly BookingDbContext _db;

    public AdminSystemFunction(BookingDbContext db)
    {
        _db = db;
    }

    [Function("Admin_System_Health")]
    public async Task<HttpResponseData> HealthAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/admin/system/health")] HttpRequestData req,
        CancellationToken ct)
    {
        var canConnect = await _db.Database.CanConnectAsync(ct);
        return await req.OkJsonAsync(new
        {
            service = "AFH.Booking",
            status = canConnect ? "Healthy" : "Degraded",
            checks = new[] { new { name = "BookingDb", status = canConnect ? "Healthy" : "Unavailable" } },
            checkedUtc = DateTime.UtcNow
        }, ct);
    }

    [Function("Admin_System_Audit")]
    public async Task<HttpResponseData> AuditAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/admin/system/audit")] HttpRequestData req,
        CancellationToken ct)
        => await AuditCoreAsync(req, ct);

    [Function("Admin_System_AuditLog")]
    public async Task<HttpResponseData> AuditLogAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/admin/system/audit-log")] HttpRequestData req,
        CancellationToken ct)
        => await AuditCoreAsync(req, ct);

    private async Task<HttpResponseData> AuditCoreAsync(HttpRequestData req, CancellationToken ct)
    {
        var fromUtc = ParseDate(req.Query("fromUtc")) ?? DateTime.UtcNow.AddDays(-7);
        var toUtc = ParseDate(req.Query("toUtc")) ?? DateTime.UtcNow.AddDays(1);
        var level = req.Query("level");
        var category = req.Query("category");
        var take = Math.Clamp(ParseInt(req.Query("take")) ?? 100, 1, 500);

        var logs = _db.ApplicationLogs.AsNoTracking()
            .Where(x => x.OccurredUtc >= fromUtc && x.OccurredUtc < toUtc);

        if (!string.IsNullOrWhiteSpace(level))
            logs = logs.Where(x => x.Level == level);
        if (!string.IsNullOrWhiteSpace(category))
            logs = logs.Where(x => x.Category == category);

        var items = await logs
            .OrderByDescending(x => x.OccurredUtc)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.OccurredUtc,
                x.Level,
                x.Category,
                x.Operation,
                x.CorrelationId,
                x.UserId,
                x.ContextId,
                x.EventType,
                x.Result,
                x.Message,
                x.ExceptionType,
                x.ExceptionMessage,
                x.PayloadJson
            })
            .ToArrayAsync(ct);

        return await req.OkJsonAsync(new { window = new { fromUtc, toUtc }, items }, ct);
    }

    [Function("Admin_System_FeatureFlags_List")]
    public async Task<HttpResponseData> ListFeatureFlagsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/admin/system/feature-flags")] HttpRequestData req,
        CancellationToken ct)
    {
        var items = await _db.FeatureFlags.AsNoTracking()
            .OrderBy(x => x.Key)
            .Select(x => new
            {
                x.Key,
                x.Name,
                x.Description,
                x.IsEnabled,
                x.UpdatedUtc,
                x.UpdatedBy
            })
            .ToArrayAsync(ct);

        return await req.OkJsonAsync(items, ct);
    }

    [Function("Admin_System_FeatureFlags_Upsert")]
    public async Task<HttpResponseData> UpsertFeatureFlagAsync(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "v1/admin/system/feature-flags/{key}")] HttpRequestData req,
        string key,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<FeatureFlagUpsertRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, "Validation");

        if (string.IsNullOrWhiteSpace(key))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "key is required.", ct, "Validation");

        var normalizedKey = key.Trim();
        var now = DateTime.UtcNow;
        var flag = await _db.FeatureFlags.FirstOrDefaultAsync(x => x.Key == normalizedKey, ct);
        if (flag is null)
        {
            flag = new FeatureFlagModel { Key = normalizedKey, CreatedUtc = now };
            _db.FeatureFlags.Add(flag);
        }

        flag.Name = string.IsNullOrWhiteSpace(body.Name) ? normalizedKey : body.Name.Trim();
        flag.Description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim();
        flag.IsEnabled = body.IsEnabled;
        flag.UpdatedBy = GetActor(req);
        flag.UpdatedUtc = now;

        await _db.SaveChangesAsync(ct);
        return await req.OkJsonAsync(new
        {
            flag.Key,
            flag.Name,
            flag.Description,
            flag.IsEnabled,
            flag.UpdatedUtc,
            flag.UpdatedBy
        }, ct);
    }

    [Function("Admin_System_FeatureFlags_Delete")]
    public async Task<HttpResponseData> DeleteFeatureFlagAsync(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "v1/admin/system/feature-flags/{key}")] HttpRequestData req,
        string key,
        CancellationToken ct)
    {
        var flag = await _db.FeatureFlags.FirstOrDefaultAsync(x => x.Key == key.Trim(), ct);
        if (flag is null)
            return await req.ProblemAsync(HttpStatusCode.NotFound, "Feature flag was not found.", ct, "NotFound");

        _db.FeatureFlags.Remove(flag);
        await _db.SaveChangesAsync(ct);
        return await req.OkJsonAsync(new { key = flag.Key, deleted = true }, ct);
    }

    private static DateTime? ParseDate(string? value)
        => DateTime.TryParse(value, out var parsed) ? parsed : null;

    private static int? ParseInt(string? value)
        => int.TryParse(value, out var parsed) ? parsed : null;

    private static string? GetActor(HttpRequestData req)
        => req.Headers.TryGetValues("x-user", out var values)
            ? values.FirstOrDefault()
            : null;

    public sealed record FeatureFlagUpsertRequest(string? Name, string? Description, bool IsEnabled);
}
