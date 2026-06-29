using AFH.Booking.Domain.Bookings;
using AFH.Booking.Infrastructure.Persistence.Models;

namespace AFH.Booking.Infrastructure.Persistence.Mapping;

internal static class BookingTransactionPersistenceMapping
{
    // ---------------------------
    // Model -> Domain
    // ---------------------------
    public static BookingTransaction ToDomain(this BookingTransactionModel m, bool includeSlots = false)
    {
        if (m is null) throw new ArgumentNullException(nameof(m));

        return BookingTransaction.Rehydrate(
            id: m.Id,
            transactionRef: m.TransactionRef,
            bookingReference: m.BookingReference,
            clientName: m.ClientName,
            clientEmail: m.ClientEmail,
            clientAddressLine1: m.ClientAddressLine1,
            clientAddressLine2: m.ClientAddressLine2,
            clientTown: m.ClientTown,
            clientCounty: m.ClientCounty,
            clientPostcode: m.ClientPostcode,
            proposedStartUtc: m.ProposedStartUtc,
            duration: TimeSpan.FromMinutes(m.DurationMinutes),
            timezone: m.Timezone,
            isRemote: m.IsRemote,
            meetingType: m.MeetingType,
            locationRef: m.LocationRef,
            status: (BookingTransactionStatus)m.Status,
            createdUtc: m.CreatedUtc,
            expiresUtc: m.ExpiresUtc,
            slots: includeSlots
                ? (m.Slots ?? Enumerable.Empty<BookingSlotModel>()).Select(slotModel => slotModel.ToDomain()).ToList()
                : null
        );
    }

    // ---------------------------
    // Domain -> Model (new model)
    // ---------------------------
    public static BookingTransactionModel ToModel(this BookingTransaction tx, bool includeSlots = false)
    {
        if (tx is null) throw new ArgumentNullException(nameof(tx));

        var m = new BookingTransactionModel
        {
            Id = tx.Id,
            TransactionRef = tx.TransactionRef,
            BookingReference = tx.BookingReference,
            ClientName = tx.ClientName,
            ClientEmail = tx.ClientEmail,
            ClientAddressLine1 = tx.ClientAddressLine1,
            ClientAddressLine2 = tx.ClientAddressLine2,
            ClientTown = tx.ClientTown,
            ClientCounty = tx.ClientCounty,
            ClientPostcode = tx.ClientPostcode,

            ProposedStartUtc = tx.ProposedStartUtc,
            DurationMinutes = (int)Math.Round(tx.Duration.TotalMinutes),
            Timezone = tx.Timezone,

            IsRemote = tx.IsRemote,
            MeetingType = tx.MeetingType,

            LocationRef = tx.LocationRef,

            Status = (int)tx.Status,
            CreatedUtc = tx.CreatedUtc,
            ExpiresUtc = tx.ExpiresUtc
        };

        if (includeSlots)
        {
            m.Slots = tx.Slots.Select(s => s.ToModel()).ToList();
        }

        return m;
    }

    // ---------------------------
    // Domain -> Model (apply changes)
    // ---------------------------
    public static void ApplyToModel(this BookingTransaction tx, BookingTransactionModel m)
    {
        if (tx is null) throw new ArgumentNullException(nameof(tx));
        if (m is null) throw new ArgumentNullException(nameof(m));

        // Id is immutable once created
        m.TransactionRef = tx.TransactionRef;
        m.BookingReference = tx.BookingReference;
        m.ClientName = tx.ClientName;
        m.ClientEmail = tx.ClientEmail;
        m.ClientAddressLine1 = tx.ClientAddressLine1;
        m.ClientAddressLine2 = tx.ClientAddressLine2;
        m.ClientTown = tx.ClientTown;
        m.ClientCounty = tx.ClientCounty;
        m.ClientPostcode = tx.ClientPostcode;

        m.ProposedStartUtc = tx.ProposedStartUtc;
        m.DurationMinutes = (int)Math.Round(tx.Duration.TotalMinutes);
        m.Timezone = tx.Timezone;

        m.IsRemote = tx.IsRemote;
        m.MeetingType = tx.MeetingType;

        m.LocationRef = tx.LocationRef;

        m.Status = (int)tx.Status;
        m.CreatedUtc = tx.CreatedUtc;
        m.ExpiresUtc = tx.ExpiresUtc;


    }
}
