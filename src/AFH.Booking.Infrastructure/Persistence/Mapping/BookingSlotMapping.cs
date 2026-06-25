using System.Text.Json;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Infrastructure.Persistence.Models;

namespace AFH.Booking.Infrastructure.Persistence.Mapping;

internal static class BookingSlotMapping
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static BookingSlot ToDomain(this BookingSlotModel m)
    {
        if (m is null) throw new ArgumentNullException(nameof(m));

        return BookingSlot.Rehydrate(
            id: m.Id,
            transactionRef: m.TransactionId,
            adviserId: m.AdviserId,
            adviserName: m.AdviserName,
            startUtc: m.StartUtc,
            endUtc: m.EndUtc,
            score: m.Score,
            scoreBreakdown: DeserializeBreakdown(m.ScoreBreakdownJson),
            locationRef: m.LocationRef,
            projectContext: m.ProjectContext,
            travelMinutes: m.TravelMinutes,
            companyBufferMinutes: m.CompanyBufferMinutes,
            distanceMiles: m.DistanceMiles,
            travelDistanceMiles: m.TravelDistanceMiles,
            sourceLocationRef: m.SourceLocationRef,
            sourcePostcode: m.SourcePostcode,
            sourceLatitude: m.SourceLatitude,
            sourceLongitude: m.SourceLongitude,
            destinationLocationRef: m.DestinationLocationRef,
            destinationPostcode: m.DestinationPostcode,
            destinationLatitude: m.DestinationLatitude,
            destinationLongitude: m.DestinationLongitude,
            travelProvider: m.TravelProvider,
            travelConfidence: m.TravelConfidence,
            travelCalculatedUtc: m.TravelCalculatedUtc,
            travelStatus: m.TravelStatus,
            travelMessage: m.TravelMessage,
            createdUtc: m.CreatedUtc
        );
    }

    public static BookingSlotModel ToModel(this BookingSlot s)
    {
        if (s is null) throw new ArgumentNullException(nameof(s));

        return new BookingSlotModel
        {
            Id = s.Id,
            TransactionId = s.TransactionId,

            AdviserId = s.AdviserId,
            AdviserName = s.AdviserName,

            StartUtc = s.StartUtc,
            EndUtc = s.EndUtc,

            Score = s.Score,
            ScoreBreakdownJson = SerializeBreakdown(s.ScoreBreakdown),
            ProjectContext = s.ProjectContext,

            TravelMinutes = s.TravelMinutes,
            CompanyBufferMinutes = s.CompanyBufferMinutes,
            DistanceMiles = s.DistanceMiles,
            TravelDistanceMiles = s.TravelDistanceMiles,
            SourceLocationRef = s.SourceLocationRef,
            SourcePostcode = s.SourcePostcode,
            SourceLatitude = s.SourceLatitude,
            SourceLongitude = s.SourceLongitude,
            DestinationLocationRef = s.DestinationLocationRef,
            DestinationPostcode = s.DestinationPostcode,
            DestinationLatitude = s.DestinationLatitude,
            DestinationLongitude = s.DestinationLongitude,
            TravelProvider = s.TravelProvider,
            TravelConfidence = s.TravelConfidence,
            TravelCalculatedUtc = s.TravelCalculatedUtc,
            TravelStatus = s.TravelStatus,
            TravelMessage = s.TravelMessage,

            LocationRef = s.LocationRef,

            CreatedUtc = s.CreatedUtc
        };
    }

    /// <summary>
    /// Updates an existing EF model instance from the domain instance.
    /// Use this when EF is tracking the entity and you want to apply changes.
    /// </summary>
    public static void ApplyToModel(this BookingSlot s, BookingSlotModel m)
    {
        if (s is null) throw new ArgumentNullException(nameof(s));
        if (m is null) throw new ArgumentNullException(nameof(m));

        // Id and TransactionId are identity/ownership - normally shouldn't change
        // m.Id = s.Id;
        // m.TransactionId = s.TransactionRef;

        m.AdviserId = s.AdviserId;
        m.AdviserName = s.AdviserName;

        m.StartUtc = s.StartUtc;
        m.EndUtc = s.EndUtc;

        m.Score = s.Score;
        m.ScoreBreakdownJson = SerializeBreakdown(s.ScoreBreakdown);
        m.ProjectContext = s.ProjectContext;

        m.TravelMinutes = s.TravelMinutes;
        m.CompanyBufferMinutes = s.CompanyBufferMinutes;
        m.DistanceMiles = s.DistanceMiles;
        m.TravelDistanceMiles = s.TravelDistanceMiles;
        m.SourceLocationRef = s.SourceLocationRef;
        m.SourcePostcode = s.SourcePostcode;
        m.SourceLatitude = s.SourceLatitude;
        m.SourceLongitude = s.SourceLongitude;
        m.DestinationLocationRef = s.DestinationLocationRef;
        m.DestinationPostcode = s.DestinationPostcode;
        m.DestinationLatitude = s.DestinationLatitude;
        m.DestinationLongitude = s.DestinationLongitude;
        m.TravelProvider = s.TravelProvider;
        m.TravelConfidence = s.TravelConfidence;
        m.TravelCalculatedUtc = s.TravelCalculatedUtc;
        m.TravelStatus = s.TravelStatus;
        m.TravelMessage = s.TravelMessage;

        m.LocationRef = s.LocationRef;

        // CreatedUtc typically shouldn't change
        // m.CreatedUtc = s.CreatedUtc;
    }

    private static IReadOnlyDictionary<string, int>? DeserializeBreakdown(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(json, JsonOptions);
        }
        catch
        {
            // Don’t blow up reads because one row has bad JSON.
            // If you want stricter behaviour, rethrow instead.
            return null;
        }
    }

    private static string? SerializeBreakdown(IReadOnlyDictionary<string, int>? dict)
    {
        if (dict is null || dict.Count == 0)
            return null;

        return JsonSerializer.Serialize(dict, JsonOptions);
    }
}
