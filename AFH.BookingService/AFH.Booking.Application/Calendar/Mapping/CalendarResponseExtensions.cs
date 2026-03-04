using AFH.Booking.Application.Calendar.Mapping;
using AFH.Booking.Contracts.Requests;
using AFH.Booking.Contracts.Responses;
using AFH.Booking.Domain.Bookings;
using AFH.Common.CalendarUtils.Contracts.Enums;
using AFH.Common.CalendarUtils.Contracts.Models;
using AFH.Common.CalendarUtils.Contracts.Requests;
using AFH.Common.CalendarUtils.Contracts.Responses;

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
            Mode = cmd.Mode.ToCalendar(),
            HoldDuration = cmd.HoldDuration,
            CreatedUtc = DateTime.UtcNow,
            Subject = cmd.Subject,
            Notes = cmd.Notes,
            TransactionId = transactionId,

            IsRemote = cmd.IsRemote,
            Categories = cmd.Categories,
            Importance = (CalendarImportance)cmd.Importance,
            Location = new Location
            {
                DisplayName = cmd.Location.DisplayName,
                AddressLine1 = cmd.Location.AddressLine1,
                City = cmd.Location.City,
                Postcode = cmd.Location.Postcode,
            }
        };
    }

    public static UpsertCalendarEventRequest ToUpsertCalendarEventRequest(this BookingsModel booking)
    {
        return new UpsertCalendarEventRequest
        {
            ExternalId = booking.Id.Value,
            UserId = booking.AdviserId,
            Subject = booking.Subject,
            StartUtc = booking.StartUtc,
            EndUtc = booking.EndUtc,
            Timezone = booking.Timezone,
            Mode = booking.Mode,
            Kind = CalendarEventKind.Hold,
            Body = booking.Notes,
            TransactionId = booking.TransactionId,

          
            IsRemote = booking.IsRemote, 
            Categories = booking.Categories?.ToList() ?? new List<string>(),
            Importance = booking.Importance,

            Location = new LocationDto
            {
                DisplayName = booking.Location.DisplayName,
                AddressLine1 = booking.Location.AddressLine1,
                City = booking.Location.City,
                Postcode = booking.Location.Postcode,
            }
        };
    }

    public static CreateHoldResponse ToCreateHoldResponse(this UpsertCalendarEventResponse source, string bookingId)
    {
        return new CreateHoldResponse
        {
            BookingId = bookingId,
            ProviderEventId = source.ProviderEventId
        };
    }
}
