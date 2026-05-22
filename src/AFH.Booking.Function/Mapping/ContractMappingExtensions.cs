using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Domain.Availability;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Domain.Common;
using System.Globalization;
using AppApprovals = AFH.Booking.Application.Models.Approvals;

namespace AFH.Booking.Function.Mapping;

public static class ContractMappingExtensions
{
    public static GetAvailabilityQuery ToQuery(this GetAvailabilityRequest req, string transactionId)
    {
        // 1️⃣ Parse preferredStartUtc (date OR date-time)
        DateTime? preferredStartUtc = null;
        bool isDateOnly = false;

        if (!string.IsNullOrWhiteSpace(req.PreferredStartUtc))
        {
            // Date-only: "2026-02-01"
            if (DateTime.TryParseExact(
                req.PreferredStartUtc,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var dateOnly))
            {
                preferredStartUtc = DateTime.SpecifyKind(dateOnly, DateTimeKind.Utc);
                isDateOnly = true;
            }
            // Date-time: ISO 8601
            else if (DateTimeOffset.TryParse(
                req.PreferredStartUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto))
            {
                preferredStartUtc = dto.UtcDateTime;
            }
            else
            {
                throw new DomainException(
                    "preferredStartUtc must be 'yyyy-MM-dd' or ISO-8601 UTC datetime.");
            }
        }

        DateTime? windowStart = AsUtc(req.Window?.StartUtc);
        DateTime? windowEnd = AsUtc(req.Window?.EndUtc);

        if (isDateOnly && preferredStartUtc.HasValue)
        {
            // Treat date as whole business day (08:00–18:00)
            windowStart = preferredStartUtc.Value.Date.AddHours(8);
            windowEnd = preferredStartUtc.Value.Date.AddHours(18);
        }

        return new GetAvailabilityQuery
        {
            // identity
            ClientId = req.ClientId,
            TransactionId = transactionId,

            // timing
            PreferredStart = preferredStartUtc ?? DateTime.UtcNow,
            WindowStartUtc = windowStart,
            WindowEndUtc = windowEnd,
            Duration = req.Duration,

            // meeting
            IsRemote = req.IsRemote,
            MeetingType = req.MeetingType,
            DestinationAddress = req.DestinationAddress is null
                ? null
                : new AFH.Booking.Domain.Location.LocationAddress
                {
                    Line1 = req.DestinationAddress.Line1,
                    Town = req.DestinationAddress.Town,
                    Postcode = req.DestinationAddress.Postcode,
                    Country = string.IsNullOrWhiteSpace(req.DestinationAddress.Country)
                        ? "UK"
                        : req.DestinationAddress.Country
                },

            // filters
            PreferredAdviserIds = req.PreferredAdviserIds,
            Regions = req.Regions,
            RequiredSkills = req.RequiredSkills,
            ExcludeAdviserIds = req.ExcludeAdviserIds,

            // knobs
            SearchHorizonMinutes = req.SearchHorizonMinutes ?? 180,
            MaxCandidates = req.MaxCandidates,
            Limit = req.Limit <= 0 ? 10 : req.Limit,
            Cursor = req.Cursor
        };
    }

    private static DateTime? AsUtc(DateTime? value)
        => value.HasValue ? AsUtc(value.Value) : null;

    private static DateTime AsUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    public static CreateHoldCommand ToCommand(this CreateHoldRequest req)
        => new()
        {
            SlotId = req.SlotId,
            TransactionRef = req.TransactionId,
        };

    public static AppApprovals.EmailBounceWebhookRequest ToApplication(this EmailBounceWebhookRequest req)
        => new()
        {
            ProviderMessageId = req.ProviderMessageId,
            RecipientEmail = req.RecipientEmail,
            ReasonCode = req.ReasonCode,
            ReasonDetail = req.ReasonDetail,
            OccurredUtc = req.OccurredUtc
        };
}
