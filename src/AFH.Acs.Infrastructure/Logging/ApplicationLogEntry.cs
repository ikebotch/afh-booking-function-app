namespace AFH.Acs.Infrastructure.Logging;

public sealed class ApplicationLogEntry
{
    public DateTime OccurredUtc { get; init; } = DateTime.UtcNow;
    public string Level { get; init; } = "Information";
    public string Category { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public string? CorrelationId { get; init; }
    public string? UserId { get; init; }
    public string? ContextId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Result { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? ExceptionType { get; init; }
    public string? ExceptionMessage { get; init; }
    public string? PayloadJson { get; init; }
}
