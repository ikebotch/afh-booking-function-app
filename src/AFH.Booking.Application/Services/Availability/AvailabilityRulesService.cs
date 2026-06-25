using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Models.Availability;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Application.Availability;

public sealed class AvailabilityRulesService : IAvailabilityRulesService
{
    private readonly IBookingHoldRepository _holds;
    private readonly AvailabilityRulesOptions _fallbackOptions;
    private readonly IAvailabilityRulesRepository _rules;
    private AvailabilityRulesOptions? _resolvedRules;

    public AvailabilityRulesService(
        IBookingHoldRepository holds,
        IAvailabilityRulesRepository rules,
        IOptions<AvailabilityRulesOptions> options)
    {
        _holds = holds;
        _rules = rules;
        _fallbackOptions = options.Value;
    }

    public async Task<AvailabilityRuleEvaluation> EvaluateAsync(
        AdviserProjectionItem adviser,
        DateTime startUtc,
        DateTime endUtc,
        double durationMinutes,
        DateTime utcNow,
        CancellationToken ct,
        string? projectContext = null)
    {
        var context = NormalizeProjectContext(projectContext);
        var rules = await GetRulesAsync(context, ct);

        var minimumDurationAllowed = durationMinutes >= Math.Max(1, rules.MinimumAppointmentMinutes);
        var workingPatternAllowed = IsWithinWorkingPattern(rules, adviser.AdviserId, startUtc, endUtc);
        var capacityAllowed = await IsWithinCapacityAsync(rules, adviser.AdviserId, startUtc, utcNow, ct);

        var allowed = minimumDurationAllowed && workingPatternAllowed && capacityAllowed;
        var reason = ResolveReason(minimumDurationAllowed, workingPatternAllowed, capacityAllowed);

        return new AvailabilityRuleEvaluation(
            allowed,
            workingPatternAllowed,
            capacityAllowed,
            minimumDurationAllowed,
            reason,
            new Dictionary<string, int>
            {
                ["minimumDurationAllowed"] = minimumDurationAllowed ? 1 : 0,
                ["workingPatternAllowed"] = workingPatternAllowed ? 1 : 0,
                ["capacityAllowed"] = capacityAllowed ? 1 : 0
            });
    }

    private async Task<AvailabilityRulesOptions> GetRulesAsync(string projectContext, CancellationToken ct)
    {
        if (_resolvedRules is not null)
            return _resolvedRules;

        _resolvedRules = await _rules.GetActiveRulesAsync(ct, projectContext) ?? _fallbackOptions;
        return _resolvedRules;
    }

    private static string NormalizeProjectContext(string? projectContext)
        => string.IsNullOrWhiteSpace(projectContext) ? "Booking" : projectContext.Trim();

    private static bool IsWithinWorkingPattern(
        AvailabilityRulesOptions rules,
        string adviserId,
        DateTime startUtc,
        DateTime endUtc)
    {
        var pattern = rules.WorkingPatterns.FirstOrDefault(x =>
            string.Equals(x.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase));

        var start = ParseTime(pattern?.Start, rules.DefaultWorkingDayStart, TimeSpan.FromHours(8));
        var end = ParseTime(pattern?.End, rules.DefaultWorkingDayEnd, TimeSpan.FromHours(17));

        var startTime = startUtc.TimeOfDay;
        var endTime = endUtc.TimeOfDay;

        return end > start && startTime >= start && endTime <= end;
    }

    private async Task<bool> IsWithinCapacityAsync(
        AvailabilityRulesOptions rules,
        string adviserId,
        DateTime slotStartUtc,
        DateTime utcNow,
        CancellationToken ct)
    {
        var capacity = rules.CapacityLimits.FirstOrDefault(x =>
            string.Equals(x.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase));

        if (capacity is null)
            return true;

        var limits = BuildCapacityWindows(capacity, rules, slotStartUtc).ToArray();
        if (limits.Length == 0)
            return true;

        foreach (var limit in limits)
        {
            var activeCount = await _holds.CountActiveOrConfirmedByAdviserAsync(
                adviserId,
                limit.WindowStartUtc,
                limit.WindowEndUtc,
                utcNow,
                ct);

            if (activeCount >= limit.MaxActiveBookings)
                return false;
        }

        return true;
    }

    private static IEnumerable<CapacityWindow> BuildCapacityWindows(
        AdviserCapacityOptions capacity,
        AvailabilityRulesOptions rules,
        DateTime slotStartUtc)
    {
        var slotDayUtc = DateTime.SpecifyKind(slotStartUtc.Date, DateTimeKind.Utc);

        if (capacity.DailyLimit.GetValueOrDefault() > 0)
            yield return new CapacityWindow(slotDayUtc, slotDayUtc.AddDays(1), capacity.DailyLimit!.Value);

        if (capacity.WeeklyLimit.GetValueOrDefault() > 0)
        {
            var weekStartUtc = GetWeekStartUtc(slotDayUtc);
            yield return new CapacityWindow(weekStartUtc, weekStartUtc.AddDays(7), capacity.WeeklyLimit!.Value);
        }

        if (capacity.MonthlyLimit.GetValueOrDefault() > 0)
        {
            var monthStartUtc = new DateTime(slotDayUtc.Year, slotDayUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            yield return new CapacityWindow(monthStartUtc, monthStartUtc.AddMonths(1), capacity.MonthlyLimit!.Value);
        }

        if (capacity.DailyLimit is null &&
            capacity.WeeklyLimit is null &&
            capacity.MonthlyLimit is null &&
            capacity.MaxActiveBookings > 0)
        {
            var windowDays = Math.Max(1, rules.CapacityWindowDays);
            yield return new CapacityWindow(slotDayUtc, slotDayUtc.AddDays(windowDays), capacity.MaxActiveBookings);
        }
    }

    private static DateTime GetWeekStartUtc(DateTime slotDayUtc)
    {
        const int monday = (int)DayOfWeek.Monday;
        var offset = (7 + (int)slotDayUtc.DayOfWeek - monday) % 7;
        return slotDayUtc.AddDays(-offset);
    }

    private static TimeSpan ParseTime(string? value, string fallback, TimeSpan defaultValue)
    {
        if (!string.IsNullOrWhiteSpace(value) && TimeSpan.TryParse(value, out var parsed))
            return parsed;

        return TimeSpan.TryParse(fallback, out parsed) ? parsed : defaultValue;
    }

    private static string? ResolveReason(bool minimumDurationAllowed, bool workingPatternAllowed, bool capacityAllowed)
    {
        if (!minimumDurationAllowed)
            return "MinimumDuration";
        if (!workingPatternAllowed)
            return "WorkingPattern";
        if (!capacityAllowed)
            return "Capacity";

        return null;
    }

    private readonly record struct CapacityWindow(DateTime WindowStartUtc, DateTime WindowEndUtc, int MaxActiveBookings);
}
