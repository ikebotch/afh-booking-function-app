namespace AFH.Notification.Application.Options;

public sealed class NotificationOutboxDispatchOptions
{
    public const string SectionName = "Notifications:Outbox";
    public const string SqlMode = "Sql";
    public const string AzureQueueMode = "AzureQueue";

    public string DispatcherMode { get; set; } = SqlMode;
    public string DispatchSchedule { get; set; } = "0 */1 * * * *";
    public int BatchSize { get; set; } = 20;
    public int MaxAttempts { get; set; } = 5;
    public int RetryDelaySeconds { get; set; } = 300;
    public int ProcessingLockSeconds { get; set; } = 300;

    public bool IsSqlMode => string.Equals(DispatcherMode, SqlMode, StringComparison.OrdinalIgnoreCase);
    public bool IsAzureQueueMode => string.Equals(DispatcherMode, AzureQueueMode, StringComparison.OrdinalIgnoreCase);

    public void Validate()
    {
        if (!IsSqlMode && !IsAzureQueueMode)
        {
            throw new InvalidOperationException(
                $"Notifications:Outbox:DispatcherMode must be '{SqlMode}' or '{AzureQueueMode}'. Current value: '{DispatcherMode}'.");
        }

        if (BatchSize <= 0)
            throw new InvalidOperationException("Notifications:Outbox:BatchSize must be greater than zero.");

        if (MaxAttempts <= 0)
            throw new InvalidOperationException("Notifications:Outbox:MaxAttempts must be greater than zero.");

        if (RetryDelaySeconds < 0)
            throw new InvalidOperationException("Notifications:Outbox:RetryDelaySeconds must be zero or greater.");

        if (ProcessingLockSeconds <= 0)
            throw new InvalidOperationException("Notifications:Outbox:ProcessingLockSeconds must be greater than zero.");
    }
}
