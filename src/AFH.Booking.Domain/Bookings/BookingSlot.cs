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
    public string ProjectContext { get; private set; } = "Booking";

    public int? TravelMinutes { get; private set; }
    public int? CompanyBufferMinutes { get; private set; }
    public decimal? DistanceMiles { get; private set; }
    public double? TravelDistanceMiles { get; private set; }
    public string? SourceLocationRef { get; private set; }
    public string? SourcePostcode { get; private set; }
    public double? SourceLatitude { get; private set; }
    public double? SourceLongitude { get; private set; }
    public string? DestinationLocationRef { get; private set; }
    public string? DestinationPostcode { get; private set; }
    public double? DestinationLatitude { get; private set; }
    public double? DestinationLongitude { get; private set; }
    public string? TravelProvider { get; private set; }
    public string? TravelConfidence { get; private set; }
    public DateTime? TravelCalculatedUtc { get; private set; }
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
               string? projectContext,
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
            ProjectContext = NormalizeProjectContext(projectContext),

            TravelMinutes = travel?.TravelMinutes,
            CompanyBufferMinutes = travel?.CompanyBufferMinutes,
            DistanceMiles = travel?.DistanceMiles,
            TravelStatus = travel is null ? "NotRequested" : "travel.IsEligible",
            TravelMessage = travel?.IneligibilityReason,

            LocationRef = locationRef,
            CreatedUtc = utcNow
        };
    }

    public void AttachTravelSnapshot(
        int? travelMinutes,
        double? distanceMiles,
        int? companyBufferMinutes,
        string? sourceLocationRef,
        string? sourcePostcode,
        double? sourceLatitude,
        double? sourceLongitude,
        string? destinationLocationRef,
        string? destinationPostcode,
        double? destinationLatitude,
        double? destinationLongitude,
        string? provider,
        string? confidence,
        DateTime? calculatedUtc)
    {
        TravelMinutes = travelMinutes;
        TravelDistanceMiles = distanceMiles;
        DistanceMiles = distanceMiles.HasValue
            ? Convert.ToDecimal(distanceMiles.Value)
            : null;
        CompanyBufferMinutes = companyBufferMinutes;

        SourceLocationRef = sourceLocationRef;
        SourcePostcode = sourcePostcode;
        SourceLatitude = sourceLatitude;
        SourceLongitude = sourceLongitude;
        DestinationLocationRef = destinationLocationRef;
        DestinationPostcode = destinationPostcode;
        DestinationLatitude = destinationLatitude;
        DestinationLongitude = destinationLongitude;

        TravelProvider = provider;
        TravelConfidence = confidence;
        TravelCalculatedUtc = calculatedUtc;
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
        => Rehydrate(
            id,
            transactionRef,
            adviserId,
            adviserName,
            startUtc,
            endUtc,
            score,
            scoreBreakdown,
            locationRef,
            null,
            travelMinutes,
            companyBufferMinutes,
            distanceMiles,
            travelStatus,
            travelMessage,
            createdUtc);

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
        string? projectContext,
        int? travelMinutes,
        int? companyBufferMinutes,
        decimal? distanceMiles,
        string? travelStatus,
        string? travelMessage,
        DateTime createdUtc)
    {
        return Rehydrate(
            id,
            transactionRef,
            adviserId,
            adviserName,
            startUtc,
            endUtc,
            score,
            scoreBreakdown,
            locationRef,
            projectContext,
            travelMinutes,
            companyBufferMinutes,
            distanceMiles,
            distanceMiles.HasValue ? Convert.ToDouble(distanceMiles.Value) : (double?)null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            travelStatus,
            travelMessage,
            createdUtc);
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
        double? travelDistanceMiles,
        string? sourceLocationRef,
        string? sourcePostcode,
        double? sourceLatitude,
        double? sourceLongitude,
        string? destinationLocationRef,
        string? destinationPostcode,
        double? destinationLatitude,
        double? destinationLongitude,
        string? travelProvider,
        string? travelConfidence,
        DateTime? travelCalculatedUtc,
        string? travelStatus,
        string? travelMessage,
        DateTime createdUtc)
        => Rehydrate(
            id,
            transactionRef,
            adviserId,
            adviserName,
            startUtc,
            endUtc,
            score,
            scoreBreakdown,
            locationRef,
            null,
            travelMinutes,
            companyBufferMinutes,
            distanceMiles,
            travelDistanceMiles,
            sourceLocationRef,
            sourcePostcode,
            sourceLatitude,
            sourceLongitude,
            destinationLocationRef,
            destinationPostcode,
            destinationLatitude,
            destinationLongitude,
            travelProvider,
            travelConfidence,
            travelCalculatedUtc,
            travelStatus,
            travelMessage,
            createdUtc);

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
        string? projectContext,
        int? travelMinutes,
        int? companyBufferMinutes,
        decimal? distanceMiles,
        double? travelDistanceMiles,
        string? sourceLocationRef,
        string? sourcePostcode,
        double? sourceLatitude,
        double? sourceLongitude,
        string? destinationLocationRef,
        string? destinationPostcode,
        double? destinationLatitude,
        double? destinationLongitude,
        string? travelProvider,
        string? travelConfidence,
        DateTime? travelCalculatedUtc,
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
            ProjectContext = NormalizeProjectContext(projectContext),
            LocationRef = locationRef,
            TravelMinutes = travelMinutes,
            CompanyBufferMinutes = companyBufferMinutes,
            DistanceMiles = distanceMiles,
            TravelDistanceMiles = travelDistanceMiles,
            SourceLocationRef = sourceLocationRef,
            SourcePostcode = sourcePostcode,
            SourceLatitude = sourceLatitude,
            SourceLongitude = sourceLongitude,
            DestinationLocationRef = destinationLocationRef,
            DestinationPostcode = destinationPostcode,
            DestinationLatitude = destinationLatitude,
            DestinationLongitude = destinationLongitude,
            TravelProvider = travelProvider,
            TravelConfidence = travelConfidence,
            TravelCalculatedUtc = travelCalculatedUtc.HasValue
                ? DateTime.SpecifyKind(travelCalculatedUtc.Value, DateTimeKind.Utc)
                : null,
            TravelStatus = travelStatus,
            TravelMessage = travelMessage,
            CreatedUtc = DateTime.SpecifyKind(createdUtc, DateTimeKind.Utc)
        };
    }

    private static string NormalizeProjectContext(string? projectContext)
        => string.IsNullOrWhiteSpace(projectContext) ? "Booking" : projectContext.Trim();
}
