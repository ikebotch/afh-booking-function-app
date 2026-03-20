using System.Text.Json.Serialization;

namespace AFH.Booking.Domain.Location.Travel;

public sealed class TravelMatrixDiagnostics
{
    public string? Source { get; set; }
    public long? ElapsedMs { get; set; }
    public string? Message { get; set; }
}