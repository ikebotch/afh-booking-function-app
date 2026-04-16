namespace AFH.Acs.Infrastructure.Logging;

public sealed class ApplicationLoggingOptions
{
    public const string SectionName = "ApplicationLogging";

    public ApplicationLogProvider Provider { get; set; } = ApplicationLogProvider.Both;

    public int MaxPayloadLength { get; set; } = 2048;

    public bool LogPayloads { get; set; } = true;
}
