using System.Text.Json;
using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Common;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Mapping;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Approvals;

public sealed class DbApprovalWorkflowService : IApprovalWorkflowService
{
    private readonly BookingDbContext _db;
    private readonly IApprovalRoutingService _routing;
    private readonly ICancellationOrchestrator _cancellation;
    private readonly IRearrangementOrchestrator _rearrangement;
    private readonly ILifecycleAuditService _audit;
    private readonly IApprovalNotificationService _notifications;
    private readonly IUnitOfWork _uow;
    private readonly JsonSerializerOptions _jsonOptions;

    public DbApprovalWorkflowService(
        BookingDbContext db,
        IApprovalRoutingService routing,
        ICancellationOrchestrator cancellation,
        IRearrangementOrchestrator rearrangement,
        ILifecycleAuditService audit,
        IApprovalNotificationService notifications,
        IUnitOfWork uow,
        JsonSerializerOptions jsonOptions)
    {
        _db = db;
        _routing = routing;
        _cancellation = cancellation;
        _rearrangement = rearrangement;
        _audit = audit;
        _notifications = notifications;
        _uow = uow;
        _jsonOptions = jsonOptions;
    }

    public async Task<ApprovalRequestResponse> CreateAsync(CreateApprovalWorkflowRequest request, CancellationToken ct)
    {
        ValidateCreate(request);

        var booking = await LoadBookingAsync(request.BookingId, ct);
        var lifecycleState = ResolveLifecycleState(booking.Hold.Status) ?? LifecycleStates.Booked;
        var routeTarget = await _routing.ResolveAsync(ct);
        var payloadJson = JsonSerializer.Serialize(new
        {
            request.ChangeType,
            request.NewSlotId,
            request.ReasonCode,
            request.ReasonDetail,
            request.RequesterId
        }, _jsonOptions);

        var model = new ApprovalRequestModel
        {
            Id = Guid.NewGuid().ToString("N"),
            BookingId = booking.Hold.Id,
            TransactionId = booking.Transaction.Id,
            ChangeType = request.ChangeType.Trim(),
            RequestedBy = request.RequestedBy.Trim(),
            RequesterId = request.RequesterId,
            Status = "Pending",
            RequestedUtc = DateTime.UtcNow,
            ReasonCode = request.ReasonCode?.Trim(),
            ReasonDetail = request.ReasonDetail?.Trim(),
            RequestedPayloadJson = payloadJson,
            ApproverTargetType = routeTarget.TargetType,
            ApproverTargetValue = routeTarget.TargetValue,
            ApproverTargetDisplayName = routeTarget.DisplayName
        };

        _db.ApprovalRequests.Add(model);
        _db.ApprovalHistory.Add(new ApprovalHistoryModel
        {
            Id = Guid.NewGuid().ToString("N"),
            ApprovalRequestId = model.Id,
            EventType = "Requested",
            ActorType = request.RequestedBy.Trim(),
            ActorId = request.RequesterId,
            Outcome = "Pending",
            Comments = request.ReasonDetail,
            OccurredUtc = model.RequestedUtc
        });

        await _audit.RecordEventAsync(new LifecycleAuditEntry(
            BookingId: model.BookingId,
            TransactionId: model.TransactionId,
            EventType: "ApprovalRequested",
            ActorType: request.RequestedBy.Trim(),
            ActorId: request.RequesterId,
            ReasonCode: request.ReasonCode,
            ReasonNotes: request.ReasonDetail,
            Before: new
            {
                approvalStatus = "None",
                bookingStatus = booking.Hold.Status.ToString()
            },
            After: new
            {
                approvalStatus = "Pending",
                changeType = model.ChangeType,
                approverTarget = routeTarget.DisplayName
            },
            OccurredUtc: model.RequestedUtc,
            CorrelationId: request.CorrelationId,
            SourceSystem: "BookingService",
            RelatedBookingId: null,
            PreviousState: lifecycleState,
            NewState: lifecycleState,
            TriggerReason: "AdviserApprovalRequestSubmitted"), ct);

        await _uow.SaveChangesAsync(ct);

        await _notifications.RecordRequestSubmittedAsync(
            routeTarget,
            booking.Hold.Id,
            booking.Transaction.Id,
            booking.Transaction.TransactionRef,
            request.RequesterId ?? request.RequestedBy,
            model.ChangeType,
            request.ReasonCode!,
            request.ReasonDetail,
            ct);

        return ToResponse(model);
    }

    public async Task<IReadOnlyList<ApprovalRequestResponse>> ListPendingAsync(CancellationToken ct)
    {
        var rows = await _db.ApprovalRequests
            .AsNoTracking()
            .Where(x => x.Status == "Pending")
            .OrderByDescending(x => x.RequestedUtc)
            .ToListAsync(ct);

        return rows.Select(ToResponse).ToList();
    }

    public async Task<ApprovalRequestResponse?> GetAsync(string requestId, CancellationToken ct)
    {
        var row = await _db.ApprovalRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, ct);
        return row is null ? null : ToResponse(row);
    }

    public async Task<ApprovalRequestResponse?> ReviewAsync(ReviewApprovalWorkflowRequest request, CancellationToken ct)
    {
        var row = await _db.ApprovalRequests.SingleOrDefaultAsync(x => x.Id == request.RequestId, ct);
        if (row is null)
            return null;

        if (!string.Equals(row.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            return ToResponse(row);

        row.Status = request.Approved ? "Approved" : "Rejected";
        row.Reviewer = request.Reviewer.Trim();
        row.ReviewNotes = request.Notes?.Trim();
        row.ReviewedUtc = DateTime.UtcNow;
        var bookingBeforeDecision = await LoadBookingAsync(row.BookingId, ct);
        var beforeDecisionState = ResolveLifecycleState(bookingBeforeDecision.Hold.Status) ?? LifecycleStates.Booked;

        _db.ApprovalHistory.Add(new ApprovalHistoryModel
        {
            Id = Guid.NewGuid().ToString("N"),
            ApprovalRequestId = row.Id,
            EventType = "Decision",
            ActorType = "Approver",
            ActorId = row.Reviewer,
            Outcome = row.Status,
            Comments = row.ReviewNotes,
            OccurredUtc = row.ReviewedUtc.Value
        });

        await _audit.RecordEventAsync(new LifecycleAuditEntry(
            BookingId: row.BookingId,
            TransactionId: row.TransactionId,
            EventType: request.Approved ? "ApprovalApproved" : "ApprovalRejected",
            ActorType: "Approver",
            ActorId: row.Reviewer,
            ReasonCode: row.ReasonCode,
            ReasonNotes: request.Notes,
            Before: new { approvalStatus = "Pending", changeType = row.ChangeType },
            After: new { approvalStatus = row.Status, approver = row.Reviewer },
            OccurredUtc: row.ReviewedUtc.Value,
            CorrelationId: request.CorrelationId,
            SourceSystem: "BookingService",
            RelatedBookingId: null,
            PreviousState: beforeDecisionState,
            NewState: beforeDecisionState,
            TriggerReason: request.Approved ? "ManagerApprovedAdviserRequest" : "ManagerRejectedAdviserRequest"), ct);

        await _uow.SaveChangesAsync(ct);

        await _notifications.RecordOutcomeAsync(
            row.BookingId,
            row.TransactionId,
            bookingBeforeDecision.Transaction.TransactionRef,
            row.RequesterId ?? row.RequestedBy,
            row.Reviewer!,
            row.Status,
            row.ChangeType,
            row.ReviewNotes,
            ct);

        if (request.Approved)
        {
            var payload = JsonSerializer.Deserialize<ApprovalPayload>(row.RequestedPayloadJson ?? "{}", _jsonOptions) ?? new ApprovalPayload();
            var execution = await ExecuteApprovedRequestAsync(row, payload, request.CorrelationId, ct);
            if (!execution.IsSuccess)
            {
                row.ExecutionError = execution.ErrorMessage;
            }
            else
            {
                row.ExecutedUtc = DateTime.UtcNow;
            }

            _db.ApprovalHistory.Add(new ApprovalHistoryModel
            {
                Id = Guid.NewGuid().ToString("N"),
                ApprovalRequestId = row.Id,
                EventType = "Execution",
                ActorType = "System",
                ActorId = "BookingService",
                Outcome = execution.IsSuccess ? "Completed" : "Failed",
                Comments = execution.ErrorMessage,
                OccurredUtc = DateTime.UtcNow
            });

            var executionState = execution.IsSuccess
                ? ResolveExecutionState(row.ChangeType)
                : beforeDecisionState;

            await _audit.RecordEventAsync(new LifecycleAuditEntry(
                BookingId: row.BookingId,
                TransactionId: row.TransactionId,
                EventType: execution.IsSuccess ? "ApprovalExecutionCompleted" : "ApprovalExecutionFailed",
                ActorType: LifecycleActors.System,
                ActorId: "BookingService",
                ReasonCode: row.ReasonCode,
                ReasonNotes: execution.ErrorMessage ?? row.ReviewNotes,
                Before: new
                {
                    approvalStatus = row.Status,
                    changeType = row.ChangeType,
                    lifecycleState = beforeDecisionState
                },
                After: new
                {
                    approvalExecution = execution.IsSuccess ? "Completed" : "Failed",
                    changeType = row.ChangeType,
                    lifecycleState = executionState
                },
                OccurredUtc: DateTime.UtcNow,
                CorrelationId: request.CorrelationId,
                SourceSystem: "BookingService",
                RelatedBookingId: row.BookingId,
                PreviousState: beforeDecisionState,
                NewState: executionState,
                TriggerReason: "ApprovedAdviserRequestExecution"), ct);

            await _uow.SaveChangesAsync(ct);
        }

        return ToResponse(row);
    }

    public async Task<bool> IsApprovedAsync(
        string requestId,
        string bookingId,
        string changeType,
        string requestedBy,
        CancellationToken ct)
    {
        return await _db.ApprovalRequests
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == requestId &&
                     x.BookingId == bookingId &&
                     x.ChangeType == changeType &&
                     x.RequestedBy == requestedBy &&
                     x.Status == "Approved",
                ct);
    }

    private async Task<Result> ExecuteApprovedRequestAsync(
        ApprovalRequestModel request,
        ApprovalPayload payload,
        string? correlationId,
        CancellationToken ct)
    {
        if (string.Equals(request.ChangeType, "Cancel", StringComparison.OrdinalIgnoreCase))
        {
            var result = await _cancellation.CancelAsync(new CancelBookingCommand
            {
                BookingId = request.BookingId,
                RequestedBy = LifecycleActors.Adviser,
                ActorId = request.RequesterId,
                ReasonCode = request.ReasonCode,
                ReasonDetail = request.ReasonDetail,
                CorrelationId = correlationId ?? request.Id
            }, sendClientNotification: true, ct);

            return result.IsSuccess
                ? Result.Ok()
                : Result.Fail(result.StatusCode, result.ErrorMessage ?? "Approval execution failed.", result.ErrorCode);
        }

        var rearrangeResult = await _rearrangement.RearrangeAsync(new RearrangeBookingCommand
        {
            BookingId = request.BookingId,
            NewSlotId = payload.NewSlotId ?? string.Empty,
            RequestedBy = LifecycleActors.Adviser,
            ActorId = request.RequesterId,
            ReasonCode = request.ReasonCode,
            ReasonDetail = request.ReasonDetail,
            CorrelationId = correlationId ?? request.Id
        }, ct);

        return rearrangeResult.IsSuccess
            ? Result.Ok()
            : Result.Fail(rearrangeResult.StatusCode, rearrangeResult.ErrorMessage ?? "Approval execution failed.", rearrangeResult.ErrorCode);
    }

    private async Task<(Domain.Bookings.BookingHold Hold, Domain.Transactions.BookingSlot Slot, Domain.Transactions.BookingTransaction Transaction)> LoadBookingAsync(string bookingId, CancellationToken ct)
    {
        var hold = await _db.Holds.AsNoTracking().SingleOrDefaultAsync(x => x.Id == bookingId, ct)
            ?? throw new InvalidOperationException($"Booking '{bookingId}' was not found.");
        var slot = await _db.BookingSlots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == hold.SlotId, ct)
            ?? throw new InvalidOperationException($"Slot '{hold.SlotId}' was not found.");
        var tx = await _db.BookingTransactions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == slot.TransactionId, ct)
            ?? throw new InvalidOperationException($"Transaction '{slot.TransactionId}' was not found.");

        return (
            AFH.Booking.Infrastructure.Persistence.Mapping.BookingHoldMapping.ToDomain(hold),
            AFH.Booking.Infrastructure.Persistence.Mapping.BookingSlotMapping.ToDomain(slot),
            tx.ToDomain(includeSlots: false));
    }

    private static void ValidateCreate(CreateApprovalWorkflowRequest request)
    {
        if (!string.Equals(request.RequestedBy, LifecycleActors.Adviser, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only adviser approval requests are supported by this workflow.");

        if (string.IsNullOrWhiteSpace(request.ReasonCode))
            throw new InvalidOperationException("reasonCode is required for adviser approval requests.");

        if (string.Equals(request.ChangeType, "Rearrange", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(request.NewSlotId))
        {
            throw new InvalidOperationException("newSlotId is required for adviser rearrangement approval requests.");
        }
    }

    private static string? ResolveLifecycleState(BookingHoldStatus status)
    {
        return status switch
        {
            BookingHoldStatus.Confirmed => LifecycleStates.Booked,
            BookingHoldStatus.Cancelled => LifecycleStates.Cancelled,
            _ => null
        };
    }

    private static string ResolveExecutionState(string changeType)
    {
        return string.Equals(changeType, "Cancel", StringComparison.OrdinalIgnoreCase)
            ? LifecycleStates.Cancelled
            : LifecycleStates.Rearranged;
    }

    private static ApprovalRequestResponse ToResponse(ApprovalRequestModel model)
    {
        var payload = string.IsNullOrWhiteSpace(model.RequestedPayloadJson)
            ? null
            : JsonSerializer.Deserialize<ApprovalPayload>(model.RequestedPayloadJson);

        return new ApprovalRequestResponse
        {
            RequestId = model.Id,
            BookingId = model.BookingId,
            TransactionId = model.TransactionId,
            ChangeType = model.ChangeType,
            RequestedBy = model.RequestedBy,
            RequesterId = model.RequesterId,
            Status = model.Status,
            RequestedUtc = model.RequestedUtc,
            ReasonCode = model.ReasonCode,
            ReasonDetail = model.ReasonDetail,
            NewSlotId = payload?.NewSlotId,
            Reviewer = model.Reviewer,
            ReviewedUtc = model.ReviewedUtc,
            ReviewNotes = model.ReviewNotes,
            ApproverTargetType = model.ApproverTargetType,
            ApproverTargetValue = model.ApproverTargetValue,
            ApproverTargetDisplayName = model.ApproverTargetDisplayName,
            RoutedTo = [model.ApproverTargetDisplayName],
            ExecutedUtc = model.ExecutedUtc
        };
    }

    private sealed class ApprovalPayload
    {
        public string? NewSlotId { get; init; }
    }
}
