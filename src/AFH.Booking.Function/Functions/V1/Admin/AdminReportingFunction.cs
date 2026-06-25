using System.Net;
using System.Text;
using AFH.Booking.Function.Http;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Function.Functions.V1.Admin;

[BookingOpenApiTag("Admin")]
public sealed class AdminReportingFunction
{
    private readonly BookingDbContext _db;

    public AdminReportingFunction(BookingDbContext db)
    {
        _db = db;
    }

    [Function("Admin_Reporting_ReportCatalogue")]
    public async Task<HttpResponseData> ReportCatalogueAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/admin/reporting/reports")] HttpRequestData req,
        CancellationToken ct)
        => await req.OkJsonAsync(new[]
        {
            new { key = "booking-summary", name = "Booking summary", category = "Bookings", endpoint = "/api/v1/admin/reports/booking-summary" },
            new { key = "approval-summary", name = "Approval summary", category = "Approvals", endpoint = "/api/v1/admin/reports/booking-summary" },
            new { key = "notification-dispatch", name = "Notification dispatch", category = "Notifications", endpoint = "/api/v1/admin/notifications/dispatch-log" }
        }, ct);

    [Function("Admin_Reports_BookingSummary")]
    public async Task<HttpResponseData> BookingSummaryAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/admin/reports/booking-summary")] HttpRequestData req,
        CancellationToken ct)
    {
        var fromUtc = ParseDate(req.Query("fromUtc")) ?? DateTime.UtcNow.AddDays(-30);
        var toUtc = ParseDate(req.Query("toUtc")) ?? DateTime.UtcNow.AddDays(1);
        if (toUtc <= fromUtc)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "toUtc must be later than fromUtc.", ct, "Validation");

        var slots = _db.BookingSlots.AsNoTracking().Where(x => x.StartUtc >= fromUtc && x.StartUtc < toUtc);
        var holds = _db.Holds.AsNoTracking().Where(x => x.CreatedUtc >= fromUtc && x.CreatedUtc < toUtc);
        var approvals = _db.ApprovalRequests.AsNoTracking().Where(x => x.RequestedUtc >= fromUtc && x.RequestedUtc < toUtc);

        var slotsByAdviser = await slots
            .GroupBy(x => new { x.AdviserId, x.AdviserName })
            .Select(x => new { x.Key.AdviserId, x.Key.AdviserName, Count = x.Count() })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToArrayAsync(ct);

        var holdsByStatus = await holds
            .GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key.ToString(), Count = x.Count() })
            .ToArrayAsync(ct);

        var approvalsByStatus = await approvals
            .GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() })
            .ToArrayAsync(ct);

        return await req.OkJsonAsync(new
        {
            window = new { fromUtc, toUtc },
            totals = new
            {
                slots = await slots.CountAsync(ct),
                holds = await holds.CountAsync(ct),
                activeHolds = await holds.CountAsync(x => x.Status == Infrastructure.Persistence.Models.HoldStatus.Active, ct),
                approvalRequests = await approvals.CountAsync(ct),
                pendingApprovalRequests = await approvals.CountAsync(x => x.Status == "Pending", ct)
            },
            slotsByAdviser,
            holdsByStatus,
            approvalsByStatus
        }, ct);
    }

    private static DateTime? ParseDate(string? value)
        => DateTime.TryParse(value, out var parsed) ? parsed : null;

    [Function("Admin_Reporting_Exports_List")]
    public async Task<HttpResponseData> ListExportsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/admin/reporting/exports")] HttpRequestData req,
        CancellationToken ct)
    {
        var exports = await _db.ApplicationLogs.AsNoTracking()
            .Where(x => x.Category == "Reporting" && x.EventType == "ExportRequested")
            .OrderByDescending(x => x.OccurredUtc)
            .Take(100)
            .Select(x => new
            {
                id = x.Id,
                requestedUtc = x.OccurredUtc,
                requestedBy = x.UserId,
                report = x.ContextId,
                status = x.Result,
                x.Message,
                x.PayloadJson
            })
            .ToArrayAsync(ct);

        return await req.OkJsonAsync(exports, ct);
    }

    [Function("Admin_Reporting_Exports_Create")]
    public async Task<HttpResponseData> RequestExportAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/admin/reporting/exports")] HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<ExportRequest>(ct);
        if (body is null || string.IsNullOrWhiteSpace(body.ReportKey))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "reportKey is required.", ct, "Validation");

        var log = new ApplicationLogModel
        {
            Id = Guid.NewGuid().ToString("N"),
            OccurredUtc = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            Level = "Information",
            Category = "Reporting",
            Operation = "Admin.Reporting.Export",
            CorrelationId = req.Headers.TryGetValues("x-correlation-id", out var values) ? values.FirstOrDefault() : null,
            UserId = req.Headers.TryGetValues("x-user", out var users) ? users.FirstOrDefault() : null,
            ContextId = body.ReportKey.Trim(),
            EventType = "ExportRequested",
            Result = "Queued",
            Message = $"Export requested for {body.ReportKey.Trim()}.",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(body)
        };

        _db.ApplicationLogs.Add(log);
        await _db.SaveChangesAsync(ct);

        return await req.OkJsonAsync(new
        {
            exportId = log.Id,
            reportKey = body.ReportKey.Trim(),
            status = log.Result,
            requestedUtc = log.OccurredUtc
        }, ct);
    }

    [Function("Admin_Reporting_Exports_Download")]
    public async Task<HttpResponseData> DownloadExportAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/admin/reporting/exports/{exportId}/download")] HttpRequestData req,
        string exportId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(exportId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "exportId is required.", ct, "Validation");

        var export = await _db.ApplicationLogs.AsNoTracking()
            .Where(x => x.Category == "Reporting" && x.EventType == "ExportRequested" && x.Id == exportId)
            .Select(x => new
            {
                x.Id,
                x.OccurredUtc,
                x.UserId,
                x.ContextId,
                x.Result,
                x.Message,
                x.PayloadJson
            })
            .FirstOrDefaultAsync(ct);

        if (export is null)
            return await req.ProblemAsync(HttpStatusCode.NotFound, "Export was not found.", ct, "NotFound");

        var csv = new StringBuilder()
            .AppendLine("ExportId,ReportKey,Status,RequestedUtc,RequestedBy,Message,PayloadJson")
            .AppendCsvLine(export.Id, export.ContextId, export.Result, export.OccurredUtc.ToString("O"), export.UserId, export.Message, export.PayloadJson)
            .ToString();

        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "text/csv; charset=utf-8");
        res.Headers.Add("Content-Disposition", $"attachment; filename=\"{export.ContextId ?? "report"}-{export.Id}.csv\"");
        await res.WriteStringAsync(csv, ct);
        return res;
    }

    public sealed record ExportRequest(string? ReportKey, string? Format, DateTime? FromUtc, DateTime? ToUtc);
}

internal static class CsvStringBuilderExtensions
{
    public static StringBuilder AppendCsvLine(this StringBuilder builder, params object?[] values)
    {
        for (var index = 0; index < values.Length; index++)
        {
            if (index > 0)
                builder.Append(',');

            builder.Append(Escape(values[index]?.ToString() ?? string.Empty));
        }

        return builder.AppendLine();
    }

    private static string Escape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
