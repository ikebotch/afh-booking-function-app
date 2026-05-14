using AFH.Booking.Application.Abstractions.Bookings.Holds;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.EmailTemplates;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Client;
using AFH.Booking.Domain.Common;

namespace AFH.Booking.Application.Holds;

public sealed class BookingCalendarService : IBookingCalendarService
{
    private readonly ICalendarGateway _calendar;
    private readonly IBookingHoldRepository _holdRepo;
    private readonly IUnitOfWork _uow;
    private readonly IHoldWindowFactory _holdWindowFactory;
    private readonly IClientDirectory _clients;

    public BookingCalendarService(
        ICalendarGateway calendar,
        IBookingHoldRepository holdRepo,
        IUnitOfWork uow,
        IHoldWindowFactory holdWindowFactory,
        IClientDirectory clients)
    {
        _calendar = calendar;
        _holdRepo = holdRepo;
        _uow = uow;
        _holdWindowFactory = holdWindowFactory;
        _clients = clients;
    }

    public async Task<Result<Unit>> CreateHoldEventAsync(
        BookingContext context,
        BookingHold hold,
        CancellationToken ct)
    {
        var slot = context.Slot;
        var tx = context.Transaction;
        var calendarUserId = context.CalendarUserId;

        var windows = _holdWindowFactory.Create(slot, tx);

        var subject = string.IsNullOrWhiteSpace(tx.MeetingType)
            ? "AFH Booking"
            : $"AFH Booking - {tx.MeetingType}";

        var calendarTemplate = HoldBookingTemplate.BuildHoldTemplate(
            slot,
            tx,
            hold,
            windows);

        CalendarLocation? calendarLocation = null;

        if (!tx.IsRemote)
        {
            var client = await _clients.GetAsync(tx.TransactionRef, ct);

            calendarLocation = CalendarLocation.CreateOrNull(
                displayName: BuildDisplayAddress(client),
                addressLine1: client?.StreetName1,
                city: client?.Town,
                postcode: client?.PostalCode);
        }

        BookingCalendarEvent calendarEvent;

        try
        {
            calendarEvent = BookingCalendarEvent.Create(
                userId: calendarUserId,
                externalId: $"hold:{hold.Id}",
                subject: subject,
                startUtc: windows.HoldStartUtc,
                endUtc: windows.HoldEndUtc,
                timezone: tx.Timezone,
                isRemote: tx.IsRemote,
                categories: new[] { "AFH Booking", "Hold" },
                body: calendarTemplate.CalendarDescription,
                providerEventId: hold.CalendarProviderEventId,
                location: tx.IsRemote ? null : calendarLocation,
                attendees: null,
                showAs: BookingShowAs.Tentative);
        }
        catch (DomainException ex)
        {
            return Result<Unit>.Fail(
                HttpStatusCode.BadRequest,
                ex.Message,
                Errors.Validation);
        }

        var providerEventId = await _calendar.CreateBookingEventAsync(
            calendarEvent,
            ct);

        if (string.IsNullOrWhiteSpace(providerEventId))
        {
            return Result<Unit>.Fail(
                HttpStatusCode.Conflict,
                "Calendar hold event was created but no provider event id was returned.",
                Errors.Conflict);
        }

        hold.AttachCalendarEvent(providerEventId);

        await _holdRepo.UpdateAsync(hold, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<Unit>.Ok(Unit.Value);
    }

    private static string? BuildDisplayAddress(ClientDirectoryItem? client)
    {
        if (client is null)
            return null;

        var parts = new[]
        {
            client.StreetName1,
            client.StreetName2,
            client.Town,
            client.County,
            client.PostalCode
        }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x!.Trim());

        var value = string.Join(", ", parts);

        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }
}