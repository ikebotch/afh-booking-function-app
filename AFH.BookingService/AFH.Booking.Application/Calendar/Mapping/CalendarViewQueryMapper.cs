using System.Globalization;
using System.Net;
using AFH.Booking.Application.Calendar.Queries;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Functions.Mapping;

public static class CalendarViewQueryMapper
{
    public static bool TryMap(
        HttpRequestData req,
        out CalendarViewQuery query,
        out HttpStatusCode status,
        out string error)
    {
        query = default!;
        status = HttpStatusCode.BadRequest;
        error = string.Empty;

        var qs = System.Web.HttpUtility.ParseQueryString(req.Url.Query);

        var adviserId = qs["adviserId"];
        var startRaw = qs["startUtc"];
        var endRaw = qs["endUtc"];
        var adviserIdsRaw = qs["adviserIds"];


        //if (string.IsNullOrWhiteSpace(adviserId))
        //{
        //    error = "adviserId query parameter is required.";
        //    return false;
        //}


        var adviserIds = adviserIdsRaw
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToArray();


        if (!DateTime.TryParse(
                startRaw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal,
                out var startUtc) ||
            !DateTime.TryParse(
                endRaw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal,
                out var endUtc))
        {
            error = "startUtc and endUtc must be valid UTC datetimes.";
            return false;
        }

        if (endUtc <= startUtc)
        {
            error = "endUtc must be after startUtc.";
            return false;
        }

        query = new CalendarViewQuery
        {
            //AdviserId = adviserId,
            AdviserIds = adviserIds,
            StartUtc = startUtc,
            EndUtc = endUtc
        };

        status = HttpStatusCode.OK;
        return true;
    }
}