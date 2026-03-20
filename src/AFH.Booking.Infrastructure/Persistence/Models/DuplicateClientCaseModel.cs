namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class DuplicateClientCaseModel
{
    public string Id { get; set; } = default!;
    public string PrimaryTransactionRef { get; set; } = default!;
    public string DuplicateTransactionRef { get; set; } = default!;
    public string Status { get; set; } = "Pending";
    public string? Notes { get; set; }
    public string? RaisedBy { get; set; }
    public DateTime RaisedUtc { get; set; }
    public string? Resolution { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedUtc { get; set; }
}
