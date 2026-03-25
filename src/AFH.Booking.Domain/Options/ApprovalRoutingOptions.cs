namespace AFH.Booking.Domain.Options;

public sealed class ApprovalRoutingOptions
{
    public const string SectionName = "ApprovalRouting";

    public string TargetType { get; set; } = "Role";
    public string TargetValue { get; set; } = "booking-approvers";
    public string DisplayName { get; set; } = "Booking Approvers";
}
