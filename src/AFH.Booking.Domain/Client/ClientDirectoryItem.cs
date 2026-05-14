namespace AFH.Booking.Domain.Client;

public sealed class ClientDirectoryItem
{
    public string? TransactionId { get; set; }
    public string? OpportunityId { get; set; }
    public string? PartnerLeadId { get; set; }

    // Address
    public string? StreetName1 { get; set; }
    public string? StreetName2 { get; set; }
    public string? County { get; set; }
    public string? Town { get; set; }
    public string? PostalCode { get; set; }

    // Person
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? DateOfBirth { get; set; }

    // Adviser / agent
    public string? AdviserId { get; set; }
    public string? AdviserName { get; set; }
    public string? AgentName { get; set; }

    // Lead meta
    public DateTime? ActiveDate { get; set; }
    public string? LeadStatus { get; set; }
    public string? LeadSource { get; set; }
    public string? OriginWebsite { get; set; }
    public string? CampaignName { get; set; }
    public string? Keyword { get; set; }
    public string? LeadType { get; set; }

    // Appointment / opportunity
    public DateTime? AppointmentDateTime { get; set; }
    public string? OpportunityName { get; set; }
    public string? ParentProduct { get; set; }
    public string? ProductName { get; set; }

    // Commercial
    public decimal? FundSize { get; set; }
    public decimal? Price { get; set; }
    public bool? IsVatApplicable { get; set; }
    public string? PartnerPerformanceDisposition { get; set; }
    public string? Notes { get; set; }

    // Integration audit
    public DateTime? IntegrationCreatedDate { get; set; }
    public DateTime? InsertedAt { get; set; }
    public string? HttpMethod { get; set; }
    public string? ApiVersion { get; set; }
}