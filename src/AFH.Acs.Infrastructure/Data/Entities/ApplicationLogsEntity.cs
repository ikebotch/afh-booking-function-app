using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AFH.Acs.Recorder.Infrastructure.Data.Entities;

public class ApplicationLogsEntity
{
    [Key]
    public long LogId { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(200)]
    public string? FunctionName { get; set; }

    [MaxLength(20)]
    public string LogLevel { get; set; } = default!;

    public string Message { get; set; } = default!;

    public string? ExceptionMessage { get; set; }

    public string? ExceptionStack { get; set; }

    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    [MaxLength(100)]
    public string? RequestId { get; set; }

    [MaxLength(200)]
    public string? EventType { get; set; }

    public string? PayloadJson { get; set; }
}