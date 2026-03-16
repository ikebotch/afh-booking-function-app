namespace AFH.Booking.Domain.Options;

public sealed class BookingPortalOptions
{
    public const string SectionName = "BookingPortal";

    /// <summary>
    /// Absolute URL template for client self-service cancel/re-arrange.
    /// Supported tokens: {bookingId}, {transactionId}, {adviserId}.
    /// </summary>
    public string? CancelOrRearrangeUrlTemplate { get; set; }
}
