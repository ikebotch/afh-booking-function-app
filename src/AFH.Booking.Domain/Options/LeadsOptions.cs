namespace AFH.Booking.Domain.Options;

public sealed class LeadsOptions
{
    public const string SectionName = "Leads";

    public string BaseUrl { get; set; } = string.Empty;
    public string? FunctionKey { get; set; }
    
    
    public string ProspectsUrl { get; set; } = string.Empty;
    public string? BearerToken { get; set; }
    public int TimeoutSeconds { get; set; } = 30;




 
    public string TokenUrl { get; init; } = default!;
    public string TenantId { get; init; } = default!;
    public string ClientId { get; init; } = default!;
    public string ClientSecret { get; init; } = default!;
    public string Scope { get; init; } = default!;
}
