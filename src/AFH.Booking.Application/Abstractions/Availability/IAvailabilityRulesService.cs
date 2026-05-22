using AFH.Booking.Application.Models.Availability;

namespace AFH.Booking.Application.Abstractions.Availability;

public interface IAvailabilityRulesService
{
    Task<AvailabilityRuleEvaluation> EvaluateAsync(
        AdviserProjectionItem adviser,
        DateTime startUtc,
        DateTime endUtc,
        double durationMinutes,
        DateTime utcNow,
        CancellationToken ct);
}
