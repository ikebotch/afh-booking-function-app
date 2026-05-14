using AFH.Booking.Domain.Location;

namespace AFH.Booking.Domain.Bookings;

public sealed class BookingSlot
{
    private BookingSlot() { }

    public string Id { get; private set; } = default!;                 // SlotId
    public string TransactionId { get; private set; } = default!;      // LeadTech transactionId OR clientId

    public string AdviserId { get; private set; } = default!;           // internal id (NOT email)
    public string AdviserName { get; private set; } = default!;         // snapshot

    public DateTime StartUtc { get; private set; }
    public DateTime EndUtc { get; private set; }

    public int Score { get; private set; }                              // 1..5
    public IReadOnlyDictionary<string, int>? ScoreBreakdown { get; private set; }

    public int? TravelMinutes { get; private set; }
    public int? CompanyBufferMinutes { get; private set; }
    public decimal? DistanceMiles { get; private set; }
    public string? TravelStatus { get; private set; }
    public string? TravelMessage { get; private set; }

    public string? LocationRef { get; private set; }                    // external location reference

    public DateTime CreatedUtc { get; private set; }

    public static BookingSlot Create(
           string id,
           string transactionId,
           string adviserId,
           string adviserName,
           DateTime startUtc,
           DateTime endUtc,
           int score,
           IReadOnlyDictionary<string, int>? scoreBreakdown,
           LocationCandidate? travel,
           string? locationRef,
           DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new DomainException("slot id is required.");

        if (string.IsNullOrWhiteSpace(transactionId))
            throw new DomainException("transactionId is required.");

        if (string.IsNullOrWhiteSpace(adviserId))
            throw new DomainException("adviserId is required.");

        if (startUtc >= endUtc)
            throw new DomainException("startUtc must be before endUtc.");

        return new BookingSlot
        {
            Id = id,
            TransactionId = transactionId,

            AdviserId = adviserId,
            AdviserName = adviserName,

            StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc),
            EndUtc = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc),

            Score = score,
            ScoreBreakdown = scoreBreakdown,

            TravelMinutes = travel?.TravelMinutes,
            CompanyBufferMinutes = travel?.CompanyBufferMinutes,
            DistanceMiles = travel?.DistanceMiles,
            TravelStatus = travel is null ? "NotRequested" : "travel.IsEligible",
            TravelMessage = travel?.IneligibilityReason,

            LocationRef = locationRef,
            CreatedUtc = utcNow
        };
    }
    public static BookingSlot Rehydrate(
        string id,
        string transactionRef,
        string adviserId,
        string adviserName,
        DateTime startUtc,
        DateTime endUtc,
        int score,
        IReadOnlyDictionary<string, int>? scoreBreakdown,
        string? locationRef,
        int? travelMinutes,
        int? companyBufferMinutes,
        decimal? distanceMiles,
        string? travelStatus,
        string? travelMessage,
        DateTime createdUtc)
    {
        return new BookingSlot
        {
            Id = id,
            TransactionId = transactionRef,
            AdviserId = adviserId,
            AdviserName = adviserName,
            StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc),
            EndUtc = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc),
            Score = score,
            ScoreBreakdown = scoreBreakdown,
            LocationRef = locationRef,
            TravelMinutes = travelMinutes,
            CompanyBufferMinutes = companyBufferMinutes,
            DistanceMiles = distanceMiles,
            TravelStatus = travelStatus,
            TravelMessage = travelMessage,
            CreatedUtc = DateTime.SpecifyKind(createdUtc, DateTimeKind.Utc)
        };
    }
}