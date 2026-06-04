using System.Text.Json;
using AFH.Booking.Application.Models.Lifecycle.Constants;
using AFH.Booking.Application.Models.Notifications;

namespace AFH.Booking.Application.Models.Lifecycle;

public sealed record BookingWorkflowNotificationRequest(
    string LifecycleEventType,
    string CorrelationId,
    string ActorType,
    IReadOnlyList<BookingNotificationRecipient> Recipients,
    IReadOnlyDictionary<string, string> Data);

public sealed record BookingWorkflowNotificationOutcome(
    string NotificationType,
    string Status,
    int RecipientCount,
    int? ChannelCount,
    string? NotificationRequestId,
    string? OutboxId,
    string? FailureCode,
    string? FailureMessageSafe)
{
    public string ToLifecycleStepStatus() => Status switch
    {
        BookingWorkflowNotificationOutcomeStatuses.Succeeded => LifecycleStepStatuses.Succeeded,
        BookingWorkflowNotificationOutcomeStatuses.Failed => LifecycleStepStatuses.Failed,
        _ => LifecycleStepStatuses.Skipped
    };

    public string? ToLifecycleStepErrorCode() =>
        Status == BookingWorkflowNotificationOutcomeStatuses.Succeeded ? null : FailureCode ?? Status;

    public string ToLifecycleStepDetails() =>
        JsonSerializer.Serialize(new
        {
            notificationType = NotificationType,
            status = Status,
            recipientCount = RecipientCount,
            channelCount = ChannelCount,
            notificationRequestId = NotificationRequestId,
            outboxId = OutboxId,
            failureCode = FailureCode,
            failureMessageSafe = FailureMessageSafe
        });

    public static BookingWorkflowNotificationOutcome FromStepResult(
        string notificationType,
        int recipientCount,
        int? channelCount,
        string stepStatus,
        string? errorCode,
        string? errorDetails)
    {
        if (stepStatus == LifecycleStepStatuses.Succeeded)
            return Succeeded(notificationType, recipientCount, channelCount);

        if (stepStatus == LifecycleStepStatuses.Failed)
            return Failed(notificationType, recipientCount, channelCount, errorCode, "Notification handoff failed.");

        var status = errorCode switch
        {
            BookingWorkflowNotificationOutcomeStatuses.SkippedNoRecipients => BookingWorkflowNotificationOutcomeStatuses.SkippedNoRecipients,
            BookingWorkflowNotificationOutcomeStatuses.SkippedNoChannels => BookingWorkflowNotificationOutcomeStatuses.SkippedNoChannels,
            BookingWorkflowNotificationOutcomeStatuses.SkippedNoMapping => BookingWorkflowNotificationOutcomeStatuses.SkippedNoMapping,
            _ => BookingWorkflowNotificationOutcomeStatuses.SkippedPolicyDisabled
        };

        return Skipped(notificationType, status, recipientCount, channelCount, errorCode, SafeSkipMessage(status, errorDetails));
    }

    public static BookingWorkflowNotificationOutcome Succeeded(
        string notificationType,
        int recipientCount,
        int? channelCount = null) =>
        new(
            notificationType,
            BookingWorkflowNotificationOutcomeStatuses.Succeeded,
            recipientCount,
            channelCount,
            null,
            null,
            null,
            null);

    public static BookingWorkflowNotificationOutcome Skipped(
        string notificationType,
        string status,
        int recipientCount,
        int? channelCount = null,
        string? failureCode = null,
        string? failureMessageSafe = null) =>
        new(
            notificationType,
            status,
            recipientCount,
            channelCount,
            null,
            null,
            failureCode ?? status,
            failureMessageSafe ?? SafeSkipMessage(status, null));

    public static BookingWorkflowNotificationOutcome NotRequested(string notificationType) =>
        Skipped(
            notificationType,
            BookingWorkflowNotificationOutcomeStatuses.SkippedNotRequested,
            0,
            null,
            BookingWorkflowNotificationOutcomeFailureCodes.NotificationNotRequested,
            "Notification handoff was not requested for this workflow.");

    public static BookingWorkflowNotificationOutcome Failed(
        string notificationType,
        int recipientCount,
        int? channelCount = null,
        string? failureCode = null,
        string? failureMessageSafe = null) =>
        new(
            notificationType,
            BookingWorkflowNotificationOutcomeStatuses.Failed,
            recipientCount,
            channelCount,
            null,
            null,
            failureCode ?? LifecycleErrorCodes.NotificationFailed,
            string.IsNullOrWhiteSpace(failureMessageSafe)
                ? "Notification handoff failed."
                : failureMessageSafe);

    private static string SafeSkipMessage(string status, string? errorDetails) =>
        status switch
        {
            BookingWorkflowNotificationOutcomeStatuses.SkippedNoRecipients => "No notification recipients resolved.",
            BookingWorkflowNotificationOutcomeStatuses.SkippedNoChannels => "No notification channels are enabled.",
            BookingWorkflowNotificationOutcomeStatuses.SkippedNoMapping => "Lifecycle event has no notification mapping.",
            BookingWorkflowNotificationOutcomeStatuses.SkippedNotRequested => "Notification handoff was not requested for this workflow.",
            _ => string.IsNullOrWhiteSpace(errorDetails)
                ? "Notification policy is disabled."
                : errorDetails
        };
}

public static class BookingWorkflowNotificationOutcomeStatuses
{
    public const string Succeeded = "Succeeded";
    public const string SkippedPolicyDisabled = "SkippedPolicyDisabled";
    public const string SkippedNoRecipients = "SkippedNoRecipients";
    public const string SkippedNoChannels = "SkippedNoChannels";
    public const string SkippedNoMapping = "SkippedNoMapping";
    public const string SkippedNotRequested = "SkippedNotRequested";
    public const string Failed = "Failed";
}

public static class BookingWorkflowNotificationOutcomeFailureCodes
{
    public const string NotificationNotRequested = "NotificationNotRequested";
}
