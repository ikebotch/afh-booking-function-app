namespace AFH.Booking.Domain.Options;

public sealed class BookingChangeAccessOptions
{
    public const string SectionName = "BookingChangeAccess";

    public string? SigningKey { get; set; }
    public bool AllowUnsignedTokensInDevelopment { get; set; }
    public int DefaultTokenValidityDays { get; set; } = 90;
}
