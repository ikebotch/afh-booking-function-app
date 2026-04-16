namespace AFH.Acs.Application.Models;

public sealed class IssueJoinTokenCommand
{
    public string GroupId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Role { get; init; } = "Client";
}
