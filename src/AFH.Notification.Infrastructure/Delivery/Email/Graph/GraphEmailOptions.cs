namespace AFH.Notification.Infrastructure.Delivery.Email.Graph;

public sealed class GraphEmailOptions
{
    public const string SectionName = "Notifications:Email:Graph";

    public bool UseManagedIdentity { get; set; } = true;
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? SenderMailbox { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SenderMailbox))
            throw new InvalidOperationException("Notifications:Email:Graph:SenderMailbox is required when Notifications:Email:ProviderName=Graph.");

        if (UseManagedIdentity)
            return;

        if (string.IsNullOrWhiteSpace(TenantId))
            throw new InvalidOperationException("Notifications:Email:Graph:TenantId is required when Notifications:Email:Graph:UseManagedIdentity=false.");

        if (string.IsNullOrWhiteSpace(ClientId))
            throw new InvalidOperationException("Notifications:Email:Graph:ClientId is required when Notifications:Email:Graph:UseManagedIdentity=false.");

        if (string.IsNullOrWhiteSpace(ClientSecret))
            throw new InvalidOperationException("Notifications:Email:Graph:ClientSecret is required when Notifications:Email:Graph:UseManagedIdentity=false.");
    }
}
