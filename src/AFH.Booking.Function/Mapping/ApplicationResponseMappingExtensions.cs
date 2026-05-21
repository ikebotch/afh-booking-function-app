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
            SlotId = response.SlotId,
            HoldExpiresUtc = response.HoldExpiresUtc,
            CompanyBufferMinutes = response.CompanyBufferMinutes
        };

    public static ContractResponses.ConfirmBookingResponse ToContract(this AppBookings.ConfirmBookingResponse response)
        => new()
        {
            BookingId = response.BookingId,
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
            CancelledUtc = response.CancelledUtc,
            Status = response.Status
        };

    public static ContractResponses.BookingDetailsResponse ToContract(this AppBookings.BookingDetailsResponse response)
        => new()
        {
            BookingId = response.BookingId,
            SlotId = response.SlotId,
            TransactionId = response.TransactionId,
            TransactionRef = response.TransactionRef,
            AdviserId = response.AdviserId,
            AdviserName = response.AdviserName,
            StartUtc = response.StartUtc,
            EndUtc = response.EndUtc,
            DurationMinutes = response.DurationMinutes,
            IsRemote = response.IsRemote,
            MeetingType = response.MeetingType,
            Status = response.Status,
            ConfirmedUtc = response.ConfirmedUtc,
            CancelledUtc = response.CancelledUtc,
            CancelReason = response.CancelReason
        };

    public static ContractResponses.RearrangeBookingResponse ToContract(this AppBookings.RearrangeBookingResponse response)
        => new()
        {
            PreviousBookingId = response.PreviousBookingId,
            NewBookingId = response.NewBookingId,
            NewSlotId = response.NewSlotId,
            PreviousAdviserId = response.PreviousAdviserId,
            PreviousAdviserName = response.PreviousAdviserName,
            PreviousStartUtc = response.PreviousStartUtc,
            PreviousEndUtc = response.PreviousEndUtc,
            NewAdviserId = response.NewAdviserId,
            NewAdviserName = response.NewAdviserName,
            NewStartUtc = response.NewStartUtc,
            NewEndUtc = response.NewEndUtc,
            NotificationSummary = response.NotificationSummary
        };

    public static ContractResponses.RearrangementOptionsResponse ToContract(this AppBookings.RearrangementOptionsResponse response)
        => new()
        {
            BookingId = response.BookingId,
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
            TransactionId = response.TransactionId,
            LifecycleEventId = response.LifecycleEventId,
            PreviousState = response.PreviousState,
            NewState = response.NewState,
            RecordedUtc = response.RecordedUtc
        };

    public static ContractResponses.ApprovalRequestResponse ToContract(this AppApprovals.ApprovalRequestResponse response)
        => new()
        {
            RequestId = response.RequestId,
            BookingId = response.BookingId,
            TransactionId = response.TransactionId,
            ChangeType = response.ChangeType,
            RequestedBy = response.RequestedBy,
            RequesterId = response.RequesterId,
            Status = response.Status,
            RequestedUtc = response.RequestedUtc,
            RoutedTo = response.RoutedTo,
            ReasonCode = response.ReasonCode,
            ReasonDetail = response.ReasonDetail,
            NewSlotId = response.NewSlotId,
            ApproverTargetType = response.ApproverTargetType,
            ApproverTargetValue = response.ApproverTargetValue,
            ApproverTargetDisplayName = response.ApproverTargetDisplayName,
            Reviewer = response.Reviewer,
            ReviewedUtc = response.ReviewedUtc,
            ReviewNotes = response.ReviewNotes,
            ExecutedUtc = response.ExecutedUtc
        };

    public static ContractResponses.EmailBounceEventResponse ToContract(this AppApprovals.EmailBounceEventResponse response)
        => new()
        {
            BounceId = response.BounceId,
            ProviderMessageId = response.ProviderMessageId,
            RecipientEmail = response.RecipientEmail,
            ReasonCode = response.ReasonCode,
            ReasonDetail = response.ReasonDetail,
            OccurredUtc = response.OccurredUtc,
            ReceivedUtc = response.ReceivedUtc
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
            RaisedUtc = response.RaisedUtc,
            Resolution = response.Resolution,
            ResolvedBy = response.ResolvedBy,
            ResolvedUtc = response.ResolvedUtc
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
            CreatedUtc = response.CreatedUtc,
            ProcessedUtc = response.ProcessedUtc,
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
            CreatedUtc = response.CreatedUtc
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
            ProcessedUtc = response.ProcessedUtc,
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
            StartUtc = dto.StartUtc,
            EndUtc = dto.EndUtc,
            Rating = dto.Rating,
            ScoreBreakdown = dto.ScoreBreakdown,
            TravelMinutes = dto.TravelMinutes,
            CompanyBufferMinutes = dto.CompanyBufferMinutes,
            DistanceMiles = dto.DistanceMiles,
            TravelStatus = dto.TravelStatus,
            TravelMessage = dto.TravelMessage,
            HoldId = dto.HoldId,
            HoldStatus = dto.HoldStatus,
            HoldExpiresUtc = dto.HoldExpiresUtc,
            HoldMessage = dto.HoldMessage
        };

    private static ContractCommon.PageResultDto<object> ToContract(this AppCommon.PageResult<object> paging)
        => new()
        {
            ReturnedCount = paging.ReturnedCount,
            PageSize = paging.PageSize,
            NextCursor = paging.NextCursor
        };
}
