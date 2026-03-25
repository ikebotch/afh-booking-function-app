namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class OperationalIssueModel
{
    public string Id { get; set; } = default!;
    public string IssueType { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string Severity { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime DetectedUtc { get; set; }
    public string? BookingId { get; set; }
    public string? TransactionId { get; set; }
    public string? TransactionRef { get; set; }
    public string? AdviserId { get; set; }
    public string? ProviderEventId { get; set; }
    public string? CorrelationId { get; set; }
    public string? MetadataJson { get; set; }
    public int EscalationCount { get; set; }
    public DateTime? LastEscalatedUtc { get; set; }
}
