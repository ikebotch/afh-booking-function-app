using AFH.Booking.Domain.Calendar;

namespace AFH.Booking.Application.Abstractions.Availability;

public interface IAvailabilityRulesService
{
    Task<AvailabilityRuleEvaluation> EvaluateAsync(
        AdviserDirectoryItem adviser,
        DateTime startUtc,
        DateTime endUtc,
        double durationMinutes,
        DateTime utcNow,
        CancellationToken ct);
}

public sealed record AvailabilityRuleEvaluation(
    bool IsAllowed,
    bool WorkingPatternAllowed,
    bool CapacityAllowed,
    bool MinimumDurationAllowed,
    string? RejectionReason,
    IReadOnlyDictionary<string, int> Audit);
