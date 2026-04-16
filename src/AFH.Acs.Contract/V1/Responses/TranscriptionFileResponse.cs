namespace AFH.Acs.Contract.V1.Responses;

public sealed class TranscriptionFileResponse
{
    public string Name { get; init; } = string.Empty;

    public string? Kind { get; init; }

    public DateTimeOffset? CreatedDateTime { get; init; }

    public long? SizeInBytes { get; init; }

    public long? ContentLength { get; init; }

    public string? Self { get; init; }

    public string? ContentUrl { get; init; }

    public string? ContentUri { get; init; }
}
