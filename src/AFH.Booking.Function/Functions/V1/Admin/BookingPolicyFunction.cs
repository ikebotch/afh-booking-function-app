using System.Net;
using System.Text.Json;
using AFH.Booking.Function.Http;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Function.Functions.V1.Admin;

[BookingOpenApiTag("Admin")]
public sealed class BookingPolicyFunction
{
    private const string CompanyBufferKey = "BookingPolicy.CompanyBufferMinutes";
    private const string TravelBufferKey = "BookingPolicy.TravelBufferMinutes";
    private const string HoldExpiryKey = "BookingPolicy.HoldExpiryMinutes";
    private const string MinimumLeadTimeKey = "BookingPolicy.MinimumLeadTimeHours";

    private readonly BookingDbContext _db;

    public BookingPolicyFunction(BookingDbContext db)
    {
        _db = db;
    }

    [Function("Admin_BookingPolicyDefaults_Get")]
    public async Task<HttpResponseData> GetDefaultsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/admin/booking/policies/defaults")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var values = await ReadPolicyValuesAsync(ct);
        return await req.OkJsonAsync(ToResponse(values), ct);
    }

    [Function("Admin_BookingPolicyDefaults_Patch")]
    public async Task<HttpResponseData> PatchDefaultsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "v1/admin/booking/policies/defaults")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<BookingPolicyDefaultsPatchRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, "Validation");

        var validation = Validate(body);
        if (validation is not null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, validation, ct, "Validation");

        var values = await ReadPolicyValuesAsync(ct);
        if (body.CompanyBufferMinutes.HasValue)
            values[CompanyBufferKey] = body.CompanyBufferMinutes.Value;
        if (body.TravelBufferMinutes.HasValue)
            values[TravelBufferKey] = body.TravelBufferMinutes.Value;
        if (body.HoldExpiryMinutes.HasValue)
            values[HoldExpiryKey] = body.HoldExpiryMinutes.Value;
        if (body.MinimumLeadTimeHours.HasValue)
            values[MinimumLeadTimeKey] = body.MinimumLeadTimeHours.Value;

        var now = DateTime.UtcNow;
        foreach (var (key, value) in values)
        {
            var state = await _db.IntegrationSyncStates.FirstOrDefaultAsync(x => x.Key == key, ct);
            if (state is null)
            {
                state = new IntegrationSyncStateModel { Key = key };
                _db.IntegrationSyncStates.Add(state);
            }

            state.Value = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            state.UpdatedUtc = now;
        }

        _db.ApplicationLogs.Add(new ApplicationLogModel
        {
            OccurredUtc = now,
            CreatedUtc = now,
            Level = "Information",
            Category = "Settings",
            Operation = "Admin.BookingPolicyDefaults.Update",
            UserId = GetActor(req),
            EventType = "BookingPolicyDefaultsUpdated",
            Result = "Succeeded",
            Message = "Booking policy defaults were updated.",
            PayloadJson = JsonSerializer.Serialize(body)
        });

        await _db.SaveChangesAsync(ct);
        return await req.OkJsonAsync(ToResponse(values, now), ct);
    }

    private async Task<Dictionary<string, int>> ReadPolicyValuesAsync(CancellationToken ct)
    {
        var keys = new[] { CompanyBufferKey, TravelBufferKey, HoldExpiryKey, MinimumLeadTimeKey };
        var stored = await _db.IntegrationSyncStates.AsNoTracking()
            .Where(x => keys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase, ct);

        return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [CompanyBufferKey] = ReadInt(stored, CompanyBufferKey, 30),
            [TravelBufferKey] = ReadInt(stored, TravelBufferKey, 0),
            [HoldExpiryKey] = ReadInt(stored, HoldExpiryKey, 15),
            [MinimumLeadTimeKey] = ReadInt(stored, MinimumLeadTimeKey, 24)
        };
    }

    private static BookingPolicyDefaultsResponse ToResponse(Dictionary<string, int> values, DateTime? updatedUtc = null)
        => new(
            values[CompanyBufferKey],
            values[TravelBufferKey],
            values[HoldExpiryKey],
            values[MinimumLeadTimeKey],
            updatedUtc,
            "IntegrationSyncStates");

    private static int ReadInt(IReadOnlyDictionary<string, string> values, string key, int fallback)
        => values.TryGetValue(key, out var value)
            && int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static string? Validate(BookingPolicyDefaultsPatchRequest request)
    {
        if (request.CompanyBufferMinutes is < 0 or > 240)
            return "companyBufferMinutes must be between 0 and 240.";
        if (request.TravelBufferMinutes is < 0 or > 240)
            return "travelBufferMinutes must be between 0 and 240.";
        if (request.HoldExpiryMinutes is < 1 or > 1440)
            return "holdExpiryMinutes must be between 1 and 1440.";
        if (request.MinimumLeadTimeHours is < 0 or > 720)
            return "minimumLeadTimeHours must be between 0 and 720.";
        return null;
    }

    private static string? GetActor(HttpRequestData req)
        => req.Headers.TryGetValues("x-afh-user-profile-id", out var profileIds)
            ? profileIds.FirstOrDefault()
            : req.Headers.TryGetValues("x-user", out var users)
                ? users.FirstOrDefault()
                : null;

    public sealed record BookingPolicyDefaultsPatchRequest(
        int? CompanyBufferMinutes,
        int? TravelBufferMinutes,
        int? HoldExpiryMinutes,
        int? MinimumLeadTimeHours);

    public sealed record BookingPolicyDefaultsResponse(
        int CompanyBufferMinutes,
        int TravelBufferMinutes,
        int HoldExpiryMinutes,
        int MinimumLeadTimeHours,
        DateTime? UpdatedUtc,
        string Source);
}
