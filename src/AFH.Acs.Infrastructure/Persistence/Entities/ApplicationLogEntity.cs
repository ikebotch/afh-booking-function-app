namespace AFH.Acs.Infrastructure.Persistence.Entities;

public sealed class ApplicationLogEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime OccurredUtc { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? UserId { get; set; }
    public string? ContextId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? PayloadJson { get; set; }
    public DateTime CreatedUtc { get; set; }
}
