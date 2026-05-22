using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Models.Availability;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Application.Availability;

public sealed class AvailabilityRulesService : IAvailabilityRulesService
{
    private readonly IBookingHoldRepository _holds;
    private readonly AvailabilityRulesOptions _options;

    public AvailabilityRulesService(
        IBookingHoldRepository holds,
        IOptions<AvailabilityRulesOptions> options)
    {
        _holds = holds;
        _options = options.Value;
    }

    public async Task<AvailabilityRuleEvaluation> EvaluateAsync(
        AdviserProjectionItem adviser,
        DateTime startUtc,
        DateTime endUtc,
        double durationMinutes,
        DateTime utcNow,
        CancellationToken ct)
    {
        var minimumDurationAllowed = durationMinutes >= Math.Max(1, _options.MinimumAppointmentMinutes);
        var workingPatternAllowed = IsWithinWorkingPattern(adviser.AdviserId, startUtc, endUtc);
        var capacityAllowed = await IsWithinCapacityAsync(adviser.AdviserId, startUtc, utcNow, ct);

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

    private bool IsWithinWorkingPattern(string adviserId, DateTime startUtc, DateTime endUtc)
    {
        var pattern = _options.WorkingPatterns.FirstOrDefault(x =>
            string.Equals(x.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase));

        var start = ParseTime(pattern?.Start, _options.DefaultWorkingDayStart, TimeSpan.FromHours(8));
        var end = ParseTime(pattern?.End, _options.DefaultWorkingDayEnd, TimeSpan.FromHours(17));

        var startTime = startUtc.TimeOfDay;
        var endTime = endUtc.TimeOfDay;

        return end > start && startTime >= start && endTime <= end;
    }

    private async Task<bool> IsWithinCapacityAsync(string adviserId, DateTime slotStartUtc, DateTime utcNow, CancellationToken ct)
    {
        var capacity = _options.CapacityLimits.FirstOrDefault(x =>
            string.Equals(x.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase));

        if (capacity is null || capacity.MaxActiveBookings <= 0)
            return true;

        var windowDays = Math.Max(1, _options.CapacityWindowDays);
        var windowStartUtc = DateTime.SpecifyKind(slotStartUtc.Date, DateTimeKind.Utc);
        var windowEndUtc = windowStartUtc.AddDays(windowDays);

        var activeCount = await _holds.CountActiveOrConfirmedByAdviserAsync(
            adviserId,
            windowStartUtc,
            windowEndUtc,
            utcNow,
            ct);

        return activeCount < capacity.MaxActiveBookings;
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
}
