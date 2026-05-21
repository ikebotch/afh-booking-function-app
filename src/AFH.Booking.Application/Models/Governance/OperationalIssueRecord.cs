namespace AFH.Booking.Application.Models.Governance;

public sealed record OperationalIssueRecord(
    string Id,
    string IssueType,
    string Code,
    string Severity,
    string Status,
    DateTime DetectedUtc,
    string? BookingId,
    string? TransactionId,
    string? TransactionRef,
    string? AdviserId,
    string? ProviderEventId,
    string? CorrelationId,
    string? MetadataJson,
    int EscalationCount,
    DateTime? LastEscalatedUtc);
