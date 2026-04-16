namespace AFH.Acs.Function.Configuration;

public sealed class ErrorEmailOptionsConfiguration
{
    public const string SectionName = "ErrorEmail";

    public string? FromAddress { get; init; }

    public string? FromDisplayName { get; init; }

    public string? ToAddresses { get; init; }

    public string? CcAddresses { get; init; }

    public string? BccAddresses { get; init; }

    public string? SubjectPrefix { get; init; }

    public bool? IncludeDetails { get; init; }
}
