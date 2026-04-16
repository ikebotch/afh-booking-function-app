namespace AFH.Acs.Application.Models;

public sealed class RecordMeetingConsentCommand
{
    public string GroupId { get; init; } = string.Empty;
    public bool Consent { get; init; }
}
