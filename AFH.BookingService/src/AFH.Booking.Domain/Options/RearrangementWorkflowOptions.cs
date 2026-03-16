namespace AFH.Booking.Domain.Options;

public sealed class RearrangementWorkflowOptions
{
    public const string SectionName = "RearrangementWorkflow";

    /// <summary>
    /// Comma separated management approver identifiers/emails (for example: Ian or a team alias).
    /// </summary>
    public string ApprovalRoutedTo { get; set; } = "Ian";

    /// <summary>
    /// Enables surfacing SMS as an additional channel in responses.
    /// </summary>
    public bool SmsEnabled { get; set; } = true;

    /// <summary>
    /// Enables bounce-back handling reminder in responses.
    /// </summary>
    public bool HandleEmailBounces { get; set; } = true;
}
