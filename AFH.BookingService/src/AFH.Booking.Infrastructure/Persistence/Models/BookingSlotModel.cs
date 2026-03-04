namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class BookingSlotModel
{
    // -------------------------
    // Identity
    // -------------------------
    public string Id { get; set; } = default!;
    public string TransactionId { get; set; } = default!;
    public string AdviserId { get; set; } = default!;
    public string AdviserName { get; set; } = default!;


    // -------------------------
    // Slot timing
    // -------------------------
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }


    // -------------------------
    // Scoring
    // -------------------------
    public int Score { get; set; }
    public string? ScoreBreakdownJson { get; set; }


    // -------------------------
    // Travel (optional, in-person only)
    // -------------------------
    public int? TravelMinutes { get; set; }
    public decimal? DistanceMiles { get; set; }


    public string? TravelStatus { get; set; }
    public string? TravelMessage { get; set; }


    // -------------------------
    // Location reference
    // -------------------------
    public string? LocationRef { get; set; }


    // -------------------------
    // Audit
    // -------------------------
    public DateTime CreatedUtc { get; set; }


    // -------------------------
    // Navigation
    // -------------------------
    public BookingTransactionModel Transaction { get; set; } = default!;
    public BookingHoldModel? Hold { get; set; }
}