namespace AFH.Booking.Infrastructure.Options;

public sealed class SharePointOptions
{
    public const string SectionName = "SharePoint";

    public string SiteId { get; set; } = string.Empty;
    public string AdvisersListId { get; set; } = string.Empty;

    // Field mappings (keep simple and explicit)
    public string AdviserIdField { get; set; } = "field_2";
    public string NameField { get; set; } = "Title";
    public string EmailField { get; set; } = "field_3";
    public string RegionField { get; set; } = "Region";


    //public string RegionField { get; set; } = "field_8";
    public string PostcodeField { get; set; } = "Postcode_x0020_Area";
    public string StatusField { get; set; } = "Adviser_x0020_Status";
    public string BioOnWebsiteField { get; set; } = "BioonWebsite_x003f_";
}
