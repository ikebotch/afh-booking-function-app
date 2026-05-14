using System.Text.Json.Serialization;

namespace AFH.Booking.Domain.Location.Travel;

public sealed class TravelMatrixResult
{
    public string? RequestId { get; set; }
    public List<LocationCandidate> Candidates { get; set; } = new();
    public TravelMatrixDiagnostics? Diagnostics { get; set; }
}

