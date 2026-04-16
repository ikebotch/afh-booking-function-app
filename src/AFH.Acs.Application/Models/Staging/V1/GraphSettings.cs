namespace AFH.Acs.Recorder.Models.V1;

public class GraphSettings
{
    public string BearerUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string GrantType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string CalendarUser { get; set; } = string.Empty;
}
