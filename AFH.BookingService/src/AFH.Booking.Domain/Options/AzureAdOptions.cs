namespace AFH.Booking.Infrastructure.Options;

public sealed class AzureAdOptions
{
    public const string SectionName = "AzureAd";
    public string TenantId { get; set; } = default!;
    public string ClientId { get; set; } = default!;
    public string ClientSecret { get; set; } = default!;

}
