namespace AFH.Booking.Domain.Options;

public sealed class LifecycleReasonOptions
{
    public const string SectionName = "Lifecycle:Reasons";

    public string Source { get; set; } = "Configuration";
    public string[] CancellationCodes { get; set; } = [];
    public string[] RearrangementCodes { get; set; } = [];
}
