using AppApprovals = AFH.Booking.Application.Models.Approvals;
using AppAvailability = AFH.Booking.Application.Models.Availability;
using AppBookings = AFH.Booking.Application.Models.Bookings;
using AppClients = AFH.Booking.Application.Models.Clients;
using AppCommon = AFH.Booking.Application.Models.Common;
using AppNotifications = AFH.Booking.Application.Models.Notifications;
using ContractCommon = AFH.Booking.Contracts.V1.Common;
using ContractDtos = AFH.Booking.Contracts.V1.Dtos;
using ContractAvailability = AFH.Booking.Contracts.V1.Dtos.Availability;
using ContractResponses = AFH.Booking.Contracts.V1.Responses;

namespace AFH.Booking.Function.Mapping;

public static class ApplicationResponseMappingExtensions
{
    public static ContractResponses.GetAvailabilityResponse ToContract(this AppAvailability.GetAvailabilityResponse response)
        => new()
        {
            TransactionId = response.TransactionId,
            Advisers = response.Advisers.Select(ToContract).ToList(),
            Paging = response.Paging.ToContract()
        };

    public static AFH.Booking.Contracts.V2.Responses.GetAvailabilityResponse ToV2Contract(this AppAvailability.GetAvailabilityResponse response)
        => new()
        {
            TransactionId = response.TransactionId,
            Items = response.Advisers.Select(ToContract).ToList(),
            Paging = response.Paging.ToContract()
        };

    public static ContractResponses.CreateBookingResponse ToContract(this AppBookings.CreateBookingResponse response)
        => new()
        {
            BookingId = response.BookingId,
            BookingReference = response.BookingReference,
            SlotId = response.SlotId,
            HoldExpiresUtc = AsUtc(response.HoldExpiresUtc),
            CompanyBufferMinutes = response.CompanyBufferMinutes
        };

    public static ContractResponses.ConfirmBookingResponse ToContract(this AppBookings.ConfirmBookingResponse response)
        => new()
        {
            BookingId = response.BookingId,
            BookingReference = response.BookingReference,
            SlotId = response.SlotId,
            TransactionId = response.TransactionId,
            TransactionRef = response.TransactionRef,
            Status = response.Status,
            LifecycleState = response.LifecycleState,
            OnlineMeetingJoinUrl = response.OnlineMeetingJoinUrl
        };

    public static ContractResponses.ReleaseHoldResponse ToContract(this AppBookings.ReleaseHoldResponse response)
        => new()
        {
            Success = response.Success,
            BookingId = response.BookingId,
            Error = response.Error is null
                ? null
                : new ContractResponses.ReleaseHoldError
                {
                    Code = response.Error.Code,
                    Message = response.Error.Message
                }
        };

    public static ContractResponses.CancelBookingResponse ToContract(this AppBookings.CancelBookingResponse response)
        => new()
        {
            BookingId = response.BookingId,
            BookingReference = response.BookingReference,
            CancelledUtc = AsUtc(response.CancelledUtc),
            Status = response.Status
        };

    public static ContractResponses.BookingDetailsResponse ToContract(this AppBookings.BookingDetailsResponse response)
        => new()
        {
            BookingId = response.BookingId,
            BookingReference = response.BookingReference,
            SlotId = response.SlotId,
            TransactionId = response.TransactionId,
            TransactionRef = response.TransactionRef,
            ClientRef = response.ClientRef,
            ClientName = response.ClientName,
            ClientEmail = response.ClientEmail,
            ClientAddressLine1 = response.ClientAddressLine1,
            ClientAddressLine2 = response.ClientAddressLine2,
            ClientTown = response.ClientTown,
            ClientCounty = response.ClientCounty,
            ClientPostcode = response.ClientPostcode,
            AdviserId = response.AdviserId,
            AdviserName = response.AdviserName,
            AdviserRegion = response.AdviserRegion,
            StartUtc = AsUtc(response.StartUtc),
            EndUtc = AsUtc(response.EndUtc),
            DurationMinutes = response.DurationMinutes,
            IsRemote = response.IsRemote,
            MeetingType = response.MeetingType,
            LocationRef = response.LocationRef,
            Status = response.Status,
            ConfirmedUtc = AsUtc(response.ConfirmedUtc),
            CancelledUtc = AsUtc(response.CancelledUtc),
            CancelReason = response.CancelReason,
            ViewBookingUrl = response.ViewBookingUrl,
            CancelBookingUrl = response.CancelBookingUrl,
            RescheduleBookingUrl = response.RescheduleBookingUrl
        };

    public static ContractResponses.AdminBookingSearchResponse ToContract(this AppBookings.AdminBookingSearchResponse response)
        => new()
        {
            Items = response.Items.Select(ToContract).ToList(),
            Page = response.Page,
            PageSize = response.PageSize,
            TotalItems = response.TotalItems,
            TotalPages = response.TotalPages
        };

    public static ContractResponses.AdminBookingSearchItem ToContract(this AppBookings.AdminBookingSearchItem item)
        => new()
        {
            BookingId = item.BookingId,
            BookingReference = item.BookingReference,
            SlotId = item.SlotId,
            TransactionId = item.TransactionId,
            TransactionRef = item.TransactionRef,
            ClientRef = item.ClientRef,
            ClientName = item.ClientName,
            ClientEmail = item.ClientEmail,
            ClientAddressLine1 = item.ClientAddressLine1,
            ClientAddressLine2 = item.ClientAddressLine2,
            ClientTown = item.ClientTown,
            ClientCounty = item.ClientCounty,
            ClientPostcode = item.ClientPostcode,
            AdviserId = item.AdviserId,
            AdviserName = item.AdviserName,
            StartUtc = AsUtc(item.StartUtc),
            EndUtc = AsUtc(item.EndUtc),
            DurationMinutes = item.DurationMinutes,
            IsRemote = item.IsRemote,
            MeetingType = item.MeetingType,
            LocationRef = item.LocationRef,
            Status = item.Status,
            CreatedUtc = AsUtc(item.CreatedUtc),
            ConfirmedUtc = AsUtc(item.ConfirmedUtc),
            CancelledUtc = AsUtc(item.CancelledUtc),
            CancelReason = item.CancelReason
        };

    public static ContractResponses.RearrangeBookingResponse ToContract(this AppBookings.RearrangeBookingResponse response)
        => new()
        {
            PreviousBookingId = response.PreviousBookingId,
            PreviousBookingReference = response.PreviousBookingReference,
            NewBookingId = response.NewBookingId,
            NewBookingReference = response.NewBookingReference,
            NewSlotId = response.NewSlotId,
            PreviousAdviserId = response.PreviousAdviserId,
            PreviousAdviserName = response.PreviousAdviserName,
            PreviousStartUtc = AsUtc(response.PreviousStartUtc),
            PreviousEndUtc = AsUtc(response.PreviousEndUtc),
            NewAdviserId = response.NewAdviserId,
            NewAdviserName = response.NewAdviserName,
            NewStartUtc = AsUtc(response.NewStartUtc),
            NewEndUtc = AsUtc(response.NewEndUtc),
            NotificationSummary = response.NotificationSummary
        };

    public static ContractResponses.RearrangementOptionsResponse ToContract(this AppBookings.RearrangementOptionsResponse response)
        => new()
        {
            BookingId = response.BookingId,
            BookingReference = response.BookingReference,
            TransactionId = response.TransactionId,
            AssignedAdviserId = response.AssignedAdviserId,
            AssignedAdviserName = response.AssignedAdviserName,
            AssignedAdviserHasAvailability = response.AssignedAdviserHasAvailability,
            AssignedAdviserOptions = response.AssignedAdviserOptions.ToContract(),
            AlternativeAdviserOptions = response.AlternativeAdviserOptions.ToContract()
        };

    public static ContractResponses.RecordNoShowResponse ToContract(this AppBookings.RecordNoShowResponse response)
        => new()
        {
            BookingId = response.BookingId,
            BookingReference = response.BookingReference,
            TransactionId = response.TransactionId,
            LifecycleEventId = response.LifecycleEventId,
            PreviousState = response.PreviousState,
            NewState = response.NewState,
            RecordedUtc = AsUtc(response.RecordedUtc)
        };

    public static ContractResponses.ApprovalRequestResponse ToContract(this AppApprovals.ApprovalRequestResponse response)
        => new()
        {
            RequestId = response.RequestId,
            BookingId = response.BookingId,
            TransactionId = response.TransactionId,
            ClientName = response.ClientName,
            AdviserName = response.AdviserName,
            MeetingType = response.MeetingType,
            Skills = response.Skills,
            ChangeType = response.ChangeType,
            RequestedBy = response.RequestedBy,
            RequesterId = response.RequesterId,
            Status = response.Status,
            RequestedUtc = AsUtc(response.RequestedUtc),
            RoutedTo = response.RoutedTo,
            ReasonCode = response.ReasonCode,
            ReasonDetail = response.ReasonDetail,
            NewSlotId = response.NewSlotId,
            Notes = response.Notes.Select(ToContract).ToList(),
            ProposedAlternativeTimes = response.ProposedAlternativeTimes.Select(ToContract).ToList(),
            ApproverTargetType = response.ApproverTargetType,
            ApproverTargetValue = response.ApproverTargetValue,
            ApproverTargetDisplayName = response.ApproverTargetDisplayName,
            Reviewer = response.Reviewer,
            ReviewedUtc = AsUtc(response.ReviewedUtc),
            ReviewNotes = response.ReviewNotes,
            ExecutedUtc = AsUtc(response.ExecutedUtc)
        };

    private static ContractResponses.ApprovalRequestNoteResponse ToContract(this AppApprovals.ApprovalRequestNoteResponse note)
        => new()
        {
            Id = note.Id,
            BookingId = note.BookingId,
            ApprovalRequestId = note.ApprovalRequestId,
            ActorType = note.ActorType,
            ActorId = note.ActorId,
            DisplayName = note.DisplayName,
            Text = note.Text,
            CreatedUtc = AsUtc(note.CreatedUtc),
            CorrelationId = note.CorrelationId
        };

    private static ContractResponses.ApprovalProposedAlternativeTimeResponse ToContract(this AppApprovals.ApprovalProposedAlternativeTime alternative)
        => new()
        {
            SlotId = alternative.SlotId,
            AdviserId = alternative.AdviserId,
            StartUtc = AsUtc(alternative.StartUtc),
            EndUtc = AsUtc(alternative.EndUtc),
            Note = alternative.Note,
            PreferenceOrder = alternative.PreferenceOrder
        };

    public static ContractResponses.EmailBounceEventResponse ToContract(this AppApprovals.EmailBounceEventResponse response)
        => new()
        {
            BounceId = response.BounceId,
            ProviderMessageId = response.ProviderMessageId,
            RecipientEmail = response.RecipientEmail,
            ReasonCode = response.ReasonCode,
            ReasonDetail = response.ReasonDetail,
            OccurredUtc = AsUtc(response.OccurredUtc),
            ReceivedUtc = AsUtc(response.ReceivedUtc)
        };

    public static ContractResponses.DuplicateClientCaseResponse ToContract(this AppClients.DuplicateClientCaseResponse response)
        => new()
        {
            CaseId = response.CaseId,
            PrimaryTransactionRef = response.PrimaryTransactionRef,
            DuplicateTransactionRef = response.DuplicateTransactionRef,
            Status = response.Status,
            Notes = response.Notes,
            RaisedBy = response.RaisedBy,
            RaisedUtc = AsUtc(response.RaisedUtc),
            Resolution = response.Resolution,
            ResolvedBy = response.ResolvedBy,
            ResolvedUtc = AsUtc(response.ResolvedUtc)
        };

    public static ContractResponses.DownstreamUpdateReconciliationResponse ToContract(this AppClients.DownstreamUpdateReconciliationResponse response)
        => new()
        {
            RequestedCount = response.RequestedCount,
            RetriedCount = response.RetriedCount,
            SucceededCount = response.SucceededCount,
            FailedCount = response.FailedCount,
            Results = response.Results.Select(ToContract).ToList()
        };

    public static ContractResponses.DownstreamUpdateResponse ToContract(this AppClients.DownstreamUpdateResponse response)
        => new()
        {
            UpdateId = response.UpdateId,
            BookingId = response.BookingId,
            ChangeType = response.ChangeType,
            Status = response.Status,
            CreatedUtc = AsUtc(response.CreatedUtc),
            ProcessedUtc = AsUtc(response.ProcessedUtc),
            ErrorMessage = response.ErrorMessage
        };

    public static ContractResponses.NotificationDispatchResponse ToContract(this AppNotifications.NotificationDispatchResponse response)
        => new()
        {
            DispatchId = response.DispatchId,
            BookingId = response.BookingId,
            EventType = response.EventType,
            SmsRequested = response.SmsRequested,
            EmailRequested = response.EmailRequested,
            SmsStatus = response.SmsStatus,
            EmailStatus = response.EmailStatus,
            ProviderMessageId = response.ProviderMessageId,
            CreatedUtc = AsUtc(response.CreatedUtc)
        };

    private static ContractResponses.DownstreamUpdateReconciliationItemResponse ToContract(this AppClients.DownstreamUpdateReconciliationItemResponse response)
        => new()
        {
            UpdateId = response.UpdateId,
            BookingId = response.BookingId,
            ChangeType = response.ChangeType,
            PreviousStatus = response.PreviousStatus,
            CurrentStatus = response.CurrentStatus,
            AttemptCount = response.AttemptCount,
            ProcessedUtc = AsUtc(response.ProcessedUtc),
            ErrorMessage = response.ErrorMessage
        };

    private static ContractAvailability.AdviserSlotsDto ToContract(this AppAvailability.AdviserSlotsDto dto)
        => new()
        {
            Id = dto.Id,
            Name = dto.Name,
            GoldStar = dto.GoldStar,
            Slots = dto.Slots.Select(ToContract).ToList()
        };

    private static ContractDtos.SlotDto ToContract(this AppAvailability.SlotDto dto)
        => new()
        {
            SlotId = dto.SlotId,
            StartUtc = AsUtc(dto.StartUtc),
            EndUtc = AsUtc(dto.EndUtc),
            Rating = dto.Rating,
            ScoreBreakdown = dto.ScoreBreakdown,
            TravelMinutes = dto.TravelMinutes,
            CompanyBufferMinutes = dto.CompanyBufferMinutes,
            DistanceMiles = dto.DistanceMiles,
            TravelStatus = dto.TravelStatus,
            TravelMessage = dto.TravelMessage,
            HoldId = dto.HoldId,
            HoldStatus = dto.HoldStatus,
            HoldExpiresUtc = AsUtc(dto.HoldExpiresUtc),
            HoldMessage = dto.HoldMessage
        };

    private static ContractCommon.PageResultDto<object> ToContract(this AppCommon.PageResult<object> paging)
        => new()
        {
            ReturnedCount = paging.ReturnedCount,
            PageSize = paging.PageSize,
            NextCursor = paging.NextCursor
        };

    private static DateTime? AsUtc(DateTime? value)
        => value.HasValue ? AsUtc(value.Value) : null;

    private static DateTime AsUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
