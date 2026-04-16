namespace AFH.Acs.Application.Abstractions.Advisers;

public sealed class AdviserInfo
{
    public string AdviserId { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string? MailboxUserId { get; init; }
}
