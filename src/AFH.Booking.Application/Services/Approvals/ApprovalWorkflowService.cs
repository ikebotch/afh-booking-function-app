using System.Text.Json;
using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Approvals;

public sealed class ApprovalWorkflowService : IApprovalWorkflowService
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IApprovalWorkflowStore _store;
    private readonly IApprovalRoutingService _routing;
    private readonly ICancellationOrchestrator _cancellation;
    private readonly IRearrangementOrchestrator _rearrangement;
    private readonly ILifecycleAuditService _audit;
    private readonly IApprovalNotificationService _notifications;
    private readonly IUnitOfWork _uow;
    private readonly IBookingReferenceGenerator? _references;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApprovalWorkflowService(
        IApprovalWorkflowStore store,
        IApprovalRoutingService routing,
        ICancellationOrchestrator cancellation,
        IRearrangementOrchestrator rearrangement,
        ILifecycleAuditService audit,
        IApprovalNotificationService notifications,
        IUnitOfWork uow,
        JsonSerializerOptions jsonOptions,
        IBookingReferenceGenerator? references = null)
    {
        _store = store;
        _routing = routing;
        _cancellation = cancellation;
        _rearrangement = rearrangement;
        _audit = audit;
        _notifications = notifications;
        _uow = uow;
        _references = references;
        _jsonOptions = jsonOptions;
    }

    public async Task<ApprovalRequestResponse> CreateAsync(
        CreateApprovalWorkflowRequest request,
        CancellationToken ct)
    {
        ValidateCreate(request);

        var actor = request.ActorContext;
        var requestedBy = actor?.ActorType ?? request.RequestedBy.Trim();
        var requesterId = actor?.ActorId ?? request.RequesterId;
        var correlationId = actor?.CorrelationId ?? request.CorrelationId;
        var requestedUtc = DateTime.UtcNow;
        var booking = await _store.LoadBookingAsync(request.BookingId, ct);
        EnsureAdviserOwnsBooking(requestedBy, requesterId, booking);
        var lifecycleState = ResolveLifecycleState(booking.Hold.Status) ?? LifecycleStates.Booked;
        var routeTarget = await _routing.ResolveAsync(ct);
        var notes = BuildCreateNotes(request, actor, booking.Hold.Id, requestedUtc, correlationId);
        var payloadJson = JsonSerializer.Serialize(new
        {
            request.ChangeType,
            request.NewSlotId,
            request.ReasonCode,
            request.ReasonDetail,
            RequesterId = requesterId,
            Notes = notes,
            ProposedAlternativeTimes = request.ProposedAlternativeTimes ?? []
        }, _jsonOptions);

        var model = new ApprovalWorkflowRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            BookingId = booking.Hold.Id,
            BookingReference = booking.Hold.Reference,
            TransactionId = booking.Transaction.Id,
            ChangeType = request.ChangeType.Trim(),
            RequestedBy = requestedBy,
            RequesterId = requesterId,
            Status = "Pending",
            RequestedUtc = requestedUtc,
            ReasonCode = request.ReasonCode?.Trim(),
            ReasonDetail = request.ReasonDetail?.Trim(),
            RequestedPayloadJson = payloadJson,
            ApproverTargetType = routeTarget.TargetType,
            ApproverTargetValue = routeTarget.TargetValue,
            ApproverTargetDisplayName = routeTarget.DisplayName
        };
        model.Reference = _references is null
            ? $"REQ-{model.Id[..Math.Min(model.Id.Length, 8)].ToUpperInvariant()}"
            : await _references.GenerateApprovalRequestReferenceAsync(model.Id, ct);

        await _store.AddRequestAsync(
            model,
            new ApprovalHistoryRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                ApprovalRequestId = model.Id,
                EventType = "Requested",
                ActorType = requestedBy,
                ActorId = requesterId,
                Outcome = "Pending",
                Comments = request.AdviserNote ?? request.ReasonDetail,
                OccurredUtc = model.RequestedUtc
            },
            ct);

        await _audit.RecordEventAsync(new LifecycleAuditEntry(
            BookingId: model.BookingId,
            TransactionId: model.TransactionId,
            EventType: "ApprovalRequested",
            ActorType: requestedBy,
            ActorId: requesterId,
            ReasonCode: request.ReasonCode,
            ReasonNotes: request.AdviserNote ?? request.ReasonDetail,
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
            CorrelationId: correlationId,
            SourceSystem: actor?.SourceApplication ?? "BookingService",
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
            requesterId ?? requestedBy,
            model.ChangeType,
            request.ReasonCode!,
            request.ReasonDetail,
            ct);

        return ToResponse(model);
    }

    private static void EnsureAdviserOwnsBooking(
        string requestedBy,
        string? requesterId,
        ApprovalBookingSnapshot booking)
    {
        if (!string.Equals(requestedBy, BookingActorContext.ActorAdviser, StringComparison.OrdinalIgnoreCase))
            return;

        if (string.IsNullOrWhiteSpace(requesterId)
            || !string.Equals(booking.Slot.AdviserId, requesterId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Signed-in adviser can only request approval for their own bookings.");
        }
    }

    public async Task<IReadOnlyList<ApprovalRequestResponse>> ListPendingAsync(CancellationToken ct)
    {
        var rows = await _store.ListPendingAsync(ct);
        return rows.Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<ApprovalRequestResponse>> ListAsync(
        ListApprovalWorkflowRequestsQuery query,
        CancellationToken ct)
    {
        var rows = await _store.ListAsync(query, ct);
        return rows.Select(ToResponse).ToList();
    }

    public async Task<ApprovalRequestResponse?> GetAsync(string requestId, CancellationToken ct)
    {
        var row = await _store.GetAsync(requestId, ct);
        return row is null ? null : ToResponse(row);
    }

    public async Task<ApprovalRequestResponse?> ReviewAsync(
        ReviewApprovalWorkflowRequest request,
        CancellationToken ct)
    {
        var row = await _store.GetForUpdateAsync(request.RequestId, ct);
        if (row is null)
            return null;

        if (!string.Equals(row.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            return ToResponse(row);

        var reviewerActor = request.ActorContext;
        var reviewerActorType = reviewerActor?.ActorType ?? "Approver";
        var reviewerId = reviewerActor?.ActorId ?? request.Reviewer.Trim();
        var reviewerDisplay = reviewerActor?.DisplayName ?? request.Reviewer.Trim();
        var correlationId = reviewerActor?.CorrelationId ?? request.CorrelationId;

        row.Status = request.Approved ? "Approved" : "Rejected";
        row.Reviewer = reviewerDisplay;
        row.ReviewNotes = request.Notes?.Trim();
        row.ReviewedUtc = DateTime.UtcNow;

        var bookingBeforeDecision = await _store.LoadBookingAsync(row.BookingId, ct);
        var beforeDecisionState = ResolveLifecycleState(bookingBeforeDecision.Hold.Status) ?? LifecycleStates.Booked;

        await _store.UpdateAsync(row, ct);
        await _store.AddHistoryAsync(new ApprovalHistoryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            ApprovalRequestId = row.Id,
            EventType = "Decision",
            ActorType = reviewerActorType,
            ActorId = reviewerId,
            Outcome = row.Status,
            Comments = row.ReviewNotes,
            OccurredUtc = row.ReviewedUtc.Value
        }, ct);

        await _audit.RecordEventAsync(new LifecycleAuditEntry(
            BookingId: row.BookingId,
            TransactionId: row.TransactionId,
            EventType: request.Approved ? "ApprovalApproved" : "ApprovalRejected",
            ActorType: reviewerActorType,
            ActorId: reviewerId,
            ReasonCode: row.ReasonCode,
            ReasonNotes: request.Notes,
            Before: new { approvalStatus = "Pending", changeType = row.ChangeType },
            After: new { approvalStatus = row.Status, approver = row.Reviewer },
            OccurredUtc: row.ReviewedUtc.Value,
            CorrelationId: correlationId,
            SourceSystem: reviewerActor?.SourceApplication ?? "BookingService",
            RelatedBookingId: null,
            PreviousState: beforeDecisionState,
            NewState: beforeDecisionState,
            TriggerReason: request.Approved ? "ManagerApprovedAdviserRequest" : "ManagerRejectedAdviserRequest"), ct);

        await _uow.SaveChangesAsync(ct);

        await _notifications.RecordOutcomeAsync(
            row.Id,
            row.BookingId,
            row.TransactionId,
            bookingBeforeDecision.Transaction.TransactionRef,
            row.RequesterId ?? row.RequestedBy,
            reviewerId ?? row.Reviewer!,
            row.Status,
            row.ChangeType,
            row.ReasonCode,
            row.ReasonDetail,
            row.ReviewNotes,
            ct);

        if (request.Approved)
            await ExecuteApprovedWorkflowAsync(request, row, beforeDecisionState, ct);

        return ToResponse(row);
    }

    public async Task<bool> IsApprovedAsync(
        string requestId,
        string bookingId,
        string changeType,
        string requestedBy,
        CancellationToken ct)
    {
        return await _store.IsApprovedAsync(requestId, bookingId, changeType, requestedBy, ct);
    }

    private async Task ExecuteApprovedWorkflowAsync(
        ReviewApprovalWorkflowRequest review,
        ApprovalWorkflowRecord row,
        string beforeDecisionState,
        CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<ApprovalPayload>(row.RequestedPayloadJson ?? "{}", _jsonOptions) ?? new ApprovalPayload();
        var execution = await ExecuteApprovedRequestAsync(row, payload, review, ct);
        if (!execution.IsSuccess)
        {
            row.ExecutionError = execution.ErrorMessage;
        }
        else
        {
            row.ExecutedUtc = DateTime.UtcNow;
        }

        await _store.UpdateAsync(row, ct);
        await _store.AddHistoryAsync(new ApprovalHistoryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            ApprovalRequestId = row.Id,
            EventType = "Execution",
            ActorType = "System",
            ActorId = "BookingService",
            Outcome = execution.IsSuccess ? "Completed" : "Failed",
            Comments = execution.ErrorMessage,
            OccurredUtc = DateTime.UtcNow
        }, ct);

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
            CorrelationId: review.ActorContext?.CorrelationId ?? review.CorrelationId,
            SourceSystem: "BookingService",
            RelatedBookingId: row.BookingId,
            PreviousState: beforeDecisionState,
            NewState: executionState,
            TriggerReason: "ApprovedAdviserRequestExecution"), ct);

        await _uow.SaveChangesAsync(ct);
    }

    private async Task<Result> ExecuteApprovedRequestAsync(
        ApprovalWorkflowRecord request,
        ApprovalPayload payload,
        ReviewApprovalWorkflowRequest review,
        CancellationToken ct)
    {
        var actor = review.ActorContext ?? BookingActorContext.ApprovalWorkflow(
            review.Reviewer,
            displayName: review.Reviewer,
            correlationId: review.CorrelationId);
        var correlationId = actor.CorrelationId ?? review.CorrelationId ?? request.Id;

        if (string.Equals(request.ChangeType, "Cancel", StringComparison.OrdinalIgnoreCase))
        {
            var result = await _cancellation.CancelAsync(new CancelBookingCommand
            {
                BookingId = request.BookingId,
                ActorContext = BookingActorContext.ApprovalWorkflow(
                    actor.ActorId,
                    displayName: actor.DisplayName,
                    correlationId: correlationId,
                    actorType: actor.ActorType,
                    permissions: actor.Permissions),
                RequestedBy = actor.ActorType,
                ActorId = actor.ActorId,
                ReasonCode = request.ReasonCode,
                ReasonDetail = request.ReasonDetail,
                CorrelationId = correlationId,
                ApprovalRequestId = request.Id
            }, sendClientNotification: true, ct);

            return result.IsSuccess
                ? Result.Ok()
                : Result.Fail(result.StatusCode, result.ErrorMessage ?? "Approval execution failed.", result.ErrorCode);
        }

        var rearrangeResult = await _rearrangement.RearrangeAsync(new RearrangeBookingCommand
        {
            BookingId = request.BookingId,
            NewSlotId = review.SelectedSlotId ?? payload.NewSlotId ?? string.Empty,
            ActorContext = BookingActorContext.ApprovalWorkflow(
                actor.ActorId,
                displayName: actor.DisplayName,
                correlationId: correlationId,
                actorType: actor.ActorType,
                permissions: actor.Permissions),
            RequestedBy = actor.ActorType,
            ActorId = actor.ActorId,
            ReasonCode = request.ReasonCode,
            ReasonDetail = request.ReasonDetail,
            CorrelationId = correlationId,
            ApprovalRequestId = request.Id
        }, ct);

        return rearrangeResult.IsSuccess
            ? Result.Ok()
            : Result.Fail(rearrangeResult.StatusCode, rearrangeResult.ErrorMessage ?? "Approval execution failed.", rearrangeResult.ErrorCode);
    }

    private static void ValidateCreate(CreateApprovalWorkflowRequest request)
    {
        var requestedBy = request.ActorContext?.ActorType ?? request.RequestedBy;
        if (!string.Equals(requestedBy, LifecycleActors.Adviser, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only adviser approval requests are supported by this workflow.");

        if (string.IsNullOrWhiteSpace(request.ReasonCode))
            throw new InvalidOperationException("reasonCode is required for adviser approval requests.");

        if (string.Equals(request.ChangeType, "Rearrange", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(request.NewSlotId) &&
            !HasProposedSlot(request.ProposedAlternativeTimes))
        {
            throw new InvalidOperationException("newSlotId or proposedAlternativeTimes is required for adviser rearrangement approval requests.");
        }
    }

    private static bool HasProposedSlot(IReadOnlyList<ApprovalProposedAlternativeTime>? alternatives)
        => alternatives?.Any(x => !string.IsNullOrWhiteSpace(x.SlotId)) == true;

    private static IReadOnlyList<ApprovalRequestNoteResponse> BuildCreateNotes(
        CreateApprovalWorkflowRequest request,
        BookingActorContext? actor,
        string bookingId,
        DateTime createdUtc,
        string? correlationId)
    {
        var text = request.AdviserNote ?? request.ReasonDetail;
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return
        [
            new ApprovalRequestNoteResponse
            {
                Id = Guid.NewGuid().ToString("N"),
                BookingId = bookingId,
                ApprovalRequestId = string.Empty,
                ActorType = actor?.ActorType ?? request.RequestedBy.Trim(),
                ActorId = actor?.ActorId ?? request.RequesterId,
                DisplayName = actor?.DisplayName,
                Text = text.Trim(),
                CreatedUtc = createdUtc,
                CorrelationId = correlationId
            }
        ];
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

    private static ApprovalRequestResponse ToResponse(ApprovalWorkflowRecord model)
    {
        var payload = string.IsNullOrWhiteSpace(model.RequestedPayloadJson)
            ? null
            : JsonSerializer.Deserialize<ApprovalPayload>(model.RequestedPayloadJson, PayloadJsonOptions);

        var notes = NormalizeNotes(payload?.Notes, model);

        return new ApprovalRequestResponse
        {
            RequestId = model.Id,
            RequestReference = model.Reference,
            BookingId = model.BookingId,
            BookingReference = model.BookingReference,
            TransactionId = model.TransactionId,
            ChangeType = model.ChangeType,
            RequestedBy = model.RequestedBy,
            RequesterId = model.RequesterId,
            Status = model.Status,
            RequestedUtc = model.RequestedUtc,
            ReasonCode = model.ReasonCode,
            ReasonDetail = model.ReasonDetail,
            NewSlotId = payload?.NewSlotId,
            Notes = notes,
            ProposedAlternativeTimes = payload?.ProposedAlternativeTimes ?? [],
            Reviewer = model.Reviewer,
            ReviewedUtc = model.ReviewedUtc,
            ReviewNotes = model.ReviewNotes,
            ApproverTargetType = model.ApproverTargetType,
            ApproverTargetValue = model.ApproverTargetValue,
            ApproverTargetDisplayName = model.ApproverTargetDisplayName,
            RoutedTo = [model.ApproverTargetDisplayName!],
            ExecutedUtc = model.ExecutedUtc
        };
    }

    private static IReadOnlyList<ApprovalRequestNoteResponse> NormalizeNotes(
        IReadOnlyList<ApprovalRequestNoteResponse>? notes,
        ApprovalWorkflowRecord model)
    {
        if (notes is null || notes.Count == 0)
            return [];

        return notes.Select(note => new ApprovalRequestNoteResponse
        {
            Id = note.Id,
            BookingId = string.IsNullOrWhiteSpace(note.BookingId) ? model.BookingId : note.BookingId,
            ApprovalRequestId = string.IsNullOrWhiteSpace(note.ApprovalRequestId) ? model.Id : note.ApprovalRequestId,
            ActorType = note.ActorType,
            ActorId = note.ActorId,
            DisplayName = note.DisplayName,
            Text = note.Text,
            CreatedUtc = note.CreatedUtc,
            CorrelationId = note.CorrelationId
        }).ToList();
    }

    private sealed class ApprovalPayload
    {
        public string? NewSlotId { get; init; }
        public IReadOnlyList<ApprovalRequestNoteResponse>? Notes { get; init; }
        public IReadOnlyList<ApprovalProposedAlternativeTime>? ProposedAlternativeTimes { get; init; }
    }
}
