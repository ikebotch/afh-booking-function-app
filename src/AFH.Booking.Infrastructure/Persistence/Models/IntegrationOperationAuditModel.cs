namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class IntegrationOperationAuditModel
{
    public string Id { get; set; } = default!;
    public string ServiceName { get; set; } = default!;
    public string FunctionName { get; set; } = default!;
    public string Method { get; set; } = default!;
    public string Path { get; set; } = default!;
    public string? QueryString { get; set; }
    public string? CorrelationId { get; set; }
    public string OperationId { get; set; } = default!;
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedUtc { get; set; }
}
