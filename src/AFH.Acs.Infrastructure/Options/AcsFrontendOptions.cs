namespace AFH.Acs.Infrastructure.Options;

public sealed class AcsFrontendOptions
{
    public const string SectionName = "Frontend";
    public string JoinBaseUrl { get; set; } = "http://localhost:5173";
}
