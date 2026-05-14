namespace AFH.Booking.Domain.Options;

public sealed class AcsOptions
{
    public const string SectionName = "Acs";

    public bool Enabled { get; set; } = false;
    public string? MeetingLinkServiceBaseUrl { get; set; }
    public string? FunctionKey { get; set; }
    public string? InternalToken { get; set; }
}
