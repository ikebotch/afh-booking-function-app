namespace AFH.Booking.Application.Models.Availability;

public sealed record AvailabilityRuleEvaluation(
    bool IsAllowed,
    bool WorkingPatternAllowed,
    bool CapacityAllowed,
    bool MinimumDurationAllowed,
    string? RejectionReason,
    IReadOnlyDictionary<string, int> Audit);
