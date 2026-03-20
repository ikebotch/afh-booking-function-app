namespace AFH.Booking.Domain.Location;

public sealed class LocationFilters
{
    public IReadOnlyList<string> Regions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> RequiredSkills { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ExcludeAdviserIds { get; set; } = Array.Empty<string>();
    public int MaxCandidates { get; set; } = 100;
    public IReadOnlyList<string> PreferredAdviserIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> AdviserIds { get; set; } = Array.Empty<string>();

}
