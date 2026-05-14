using AFH.Booking.Domain.Location.Travel;

namespace AFH.Booking.Domain.Location;

public sealed class LocationCandidate
{
    public string AdviserId { get; set; } = default!;
    public string AdviserName { get; set; } = default!;
    public string MailboxUserId { get; set; } = string.Empty;
    public bool GoldStar { get; set; }
    public string? Region { get; set; }
    public int? TravelMinutes { get; set; }
    public decimal? DistanceMiles { get; set; }


    public bool IsEligible { get; set; } = true;
    public string? IneligibilityReason { get; set; }



    public bool Preferred { get; set; }
    public string Availability { get; set; } = "Unknown"; // ToDo: enum
    public ProposedSlot ProposedSlotUtc { get; set; } = new();
    public CoverageInfo Coverage { get; set; } = new();
    public TravelToClient TravelToClient { get; set; } = new();
    public TravelToBase TravelToBase { get; set; } = new();
    public TravelToNearestOffice TravelToNearestOffice { get; set; } = new();
    public BufferInfo Buffers { get; set; } = new();
    public TravelSnapshotResult? TravelSnapshot { get; set; }
    public int? CompanyBufferMinutes { get; set; }
    public List<string> Reasons { get; set; } = new();
    public int Rank { get; set; }
    public double? Score { get; set; }
}
