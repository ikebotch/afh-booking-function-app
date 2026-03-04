using AFH.Booking.Contracts.Requests;
using AFH.Booking.Domain.Bookings;

public static class BookingMappings
{
    public static BookingsModel ToBookingModel(this CreateHoldRequest cmd, string transactionId)
    {
        return new BookingsModel
        {
            AdviserId = cmd.AdviserId,
            CustomerId = cmd.CustomerId,
            StartUtc = cmd.StartUtc,
            EndUtc = cmd.EndUtc,
            Timezone = cmd.Timezone,
            Mode = cmd.Mode.ToDomain(),
            HoldDuration = cmd.HoldDuration,
            CreatedUtc = DateTime.UtcNow,
            Subject = cmd.Subject,
            Notes = cmd.Notes,
            TransactionId = transactionId,
            IsRemote = cmd.IsRemote,
            Categories = cmd.Categories,
            Importance = cmd.Importance.ToDomain(),
            Location = new Location
            {
                DisplayName = cmd.Location.DisplayName,
                AddressLine1 = cmd.Location.AddressLine1,
                City = cmd.Location.City,
                Postcode = cmd.Location.Postcode,
            }
        };
    }

    private static MeetingMode ToDomain(this AFH.Booking.Contracts.MeetingMode mode)
        => mode switch
        {
            AFH.Booking.Contracts.MeetingMode.Remote => MeetingMode.Remote,
            AFH.Booking.Contracts.MeetingMode.InPerson => MeetingMode.InPerson,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

    private static CalendarImportance ToDomain(this AFH.Booking.Contracts.Dtos.CalendarImportance importance)
        => importance switch
        {
            AFH.Booking.Contracts.Dtos.CalendarImportance.Low => CalendarImportance.Low,
            AFH.Booking.Contracts.Dtos.CalendarImportance.Normal => CalendarImportance.Normal,
            AFH.Booking.Contracts.Dtos.CalendarImportance.High => CalendarImportance.High,
            _ => throw new ArgumentOutOfRangeException(nameof(importance), importance, null)
        };
}
