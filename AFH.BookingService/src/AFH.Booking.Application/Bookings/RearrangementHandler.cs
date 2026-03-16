using AFH.Booking.Application.Abstractions;
using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Application.Bookings.Queries;
using AFH.Booking.Application.Common;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace AFH.Booking.Application.Bookings;

public sealed class RearrangementHandler : IRearrangementHandler
{
    private static readonly IReadOnlyList<ReasonOptionDto> RearrangementReasons =
    [
        new ReasonOptionDto { Code = "CLIENT_REQUEST", Label = "Client requested a different time" },
        new ReasonOptionDto { Code = "ADVISER_UNAVAILABLE", Label = "Adviser became unavailable" },
        new ReasonOptionDto { Code = "LEAD_TECH_REQUEST", Label = "Lead Tech requested a change" },
        new ReasonOptionDto { Code = "OTHER", Label = "Other" }
    ];

    private static readonly IReadOnlyList<ReasonOptionDto> CancellationReasons =
    [
        new ReasonOptionDto { Code = "NO_LONGER_REQUIRED", Label = "Meeting no longer required" },
        new ReasonOptionDto { Code = "CLIENT_UNAVAILABLE", Label = "Client unavailable" },
        new ReasonOptionDto { Code = "ADVISER_UNAVAILABLE", Label = "Adviser unavailable" },
        new ReasonOptionDto { Code = "OTHER", Label = "Other" }
    ];

    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _transactions;
    private readonly IAvailabilityHandler _availability;
    private readonly ICreateBookingHandler _createBooking;
    private readonly IConfirmBookingHandler _confirmBooking;
    private readonly ICancelBookingHandler _cancelBooking;
    private readonly RearrangementWorkflowOptions _workflowOptions;
    private readonly ILogger<RearrangementHandler> _logger;

    public RearrangementHandler(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        IAvailabilityHandler availability,
        ICreateBookingHandler createBooking,
        IConfirmBookingHandler confirmBooking,
        ICancelBookingHandler cancelBooking,
        IOptions<RearrangementWorkflowOptions> workflowOptions,
        ILogger<RearrangementHandler> logger)
    {
        _holds = holds;
        _slots = slots;
        _transactions = transactions;
        _availability = availability;
        _createBooking = createBooking;
        _confirmBooking = confirmBooking;
        _cancelBooking = cancelBooking;
        _workflowOptions = workflowOptions.Value;
        _logger = logger;
    }

    public async Task<Result<GetRearrangementOptionsResponse>> GetOptionsAsync(
        string bookingId,
        GetRearrangementOptionsRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingId))
            return Result<GetRearrangementOptionsResponse>.Fail(HttpStatusCode.BadRequest, "bookingId is required.", Errors.Validation);

        var hold = await _holds.GetAsync(bookingId.Trim(), ct);
        if (hold is null)
            return Result<GetRearrangementOptionsResponse>.NotFound($"Hold '{bookingId}' was not found.");

        var currentSlot = await _slots.GetAsync(hold.SlotId, ct);
        if (currentSlot is null)
            return Result<GetRearrangementOptionsResponse>.Fail(HttpStatusCode.Conflict, "Current booking slot was not found.", Errors.Conflict);

        var tx = await _transactions.GetAsync(currentSlot.TransactionId, ct);
        if (tx is null)
            return Result<GetRearrangementOptionsResponse>.Fail(HttpStatusCode.Conflict, "Booking transaction was not found.", Errors.Conflict);

        var preferredStart = currentSlot.StartUtc;
        if (!TryParsePreferredStartUtc(request.PreferredStartUtc, out preferredStart))
            return Result<GetRearrangementOptionsResponse>.Fail(
                HttpStatusCode.BadRequest,
                "preferredStartUtc must be either 'yyyy-MM-dd' or ISO-8601 UTC.",
                Errors.Validation);

        var availabilityQuery = new GetAvailabilityQuery
        {
            TransactionId = tx.TransactionRef,
            PreferredStart = preferredStart,
            Duration = request.DurationMinutes is > 0 ? request.DurationMinutes.Value : tx.Duration.TotalMinutes,
            IsRemote = tx.IsRemote,
            MeetingType = tx.MeetingType ?? "Review",
            WindowStartUtc = request.Window?.StartUtc,
            WindowEndUtc = request.Window?.EndUtc,
            PreferredAdviserIds = new[] { currentSlot.AdviserId },
            Limit = request.Limit <= 0 ? 10 : request.Limit
        };

        var availability = await _availability.HandleAsync(availabilityQuery, ct);
        if (!availability.IsSuccess)
            return Result<GetRearrangementOptionsResponse>.Fail(
                availability.StatusCode,
                availability.ErrorMessage ?? "Unable to build rearrangement options.",
                availability.ErrorCode);

        var payload = availability.Value!;
        if (!request.IncludeAlternativeAdvisers)
        {
            payload.Advisers = payload.Advisers
                .Where(a => string.Equals(a.Id, currentSlot.AdviserId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var currentAdviserHasAvailability = payload.Advisers.Any(a => string.Equals(a.Id, currentSlot.AdviserId, StringComparison.OrdinalIgnoreCase));

        var considerations = BuildConsiderations();

        return Result<GetRearrangementOptionsResponse>.Ok(new GetRearrangementOptionsResponse
        {
            BookingId = hold.Id,
            CurrentAdviserId = currentSlot.AdviserId,
            CurrentAdviserName = currentSlot.AdviserName,
            CurrentStartUtc = currentSlot.StartUtc,
            CurrentEndUtc = currentSlot.EndUtc,
            CurrentAdviserHasAvailability = currentAdviserHasAvailability,
            RequiresAlternativeAdviserSelection = !currentAdviserHasAvailability,
            RearrangementReasons = RearrangementReasons,
            CancellationReasons = CancellationReasons,
            Considerations = considerations,
            Availability = payload
        });
    }

    public async Task<Result<ExecuteRearrangementResponse>> ExecuteAsync(
        string bookingId,
        ExecuteRearrangementRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingId))
            return Result<ExecuteRearrangementResponse>.Fail(HttpStatusCode.BadRequest, "bookingId is required.", Errors.Validation);

        if (string.IsNullOrWhiteSpace(request.NewSlotId))
            return Result<ExecuteRearrangementResponse>.Fail(HttpStatusCode.BadRequest, "newSlotId is required.", Errors.Validation);

        if (!TryParseActorRole(request.ActorRole, out var actorRole))
            return Result<ExecuteRearrangementResponse>.Fail(HttpStatusCode.BadRequest, "actorRole must be one of Client, Adviser, LeadTech, Management.", Errors.Validation);

        if (actorRole == "Adviser" && !request.ApprovalGranted)
        {
            return Result<ExecuteRearrangementResponse>.Ok(new ExecuteRearrangementResponse
            {
                PreviousBookingId = bookingId,
                Status = "PendingApproval",
                ApprovalRequired = true,
                RoutedTo = _workflowOptions.ApprovalRoutedTo,
                ChangeSummary =
                [
                    "Adviser-initiated re-arrangement requires management approval.",
                    $"Request routed to {_workflowOptions.ApprovalRoutedTo}."
                ],
                NotificationChannels = BuildNotificationChannels()
            });
        }

        if (actorRole == "Adviser" && string.IsNullOrWhiteSpace(request.ApprovedBy))
            return Result<ExecuteRearrangementResponse>.Fail(HttpStatusCode.BadRequest, "approvedBy is required when adviser approval is granted.", Errors.Validation);

        var hold = await _holds.GetAsync(bookingId.Trim(), ct);
        if (hold is null)
            return Result<ExecuteRearrangementResponse>.NotFound($"Hold '{bookingId}' was not found.");

        var oldSlot = await _slots.GetAsync(hold.SlotId, ct);
        if (oldSlot is null)
            return Result<ExecuteRearrangementResponse>.Fail(HttpStatusCode.Conflict, "Current booking slot was not found.", Errors.Conflict);

        var createResult = await _createBooking.HandleAsync(new CreateHoldCommand
        {
            SlotId = request.NewSlotId.Trim(),
            TransactionRef = request.TransactionRef
        }, ct);

        if (!createResult.IsSuccess || createResult.Value is null)
            return Result<ExecuteRearrangementResponse>.Fail(
                createResult.StatusCode,
                createResult.ErrorMessage ?? "Unable to create replacement booking hold.",
                createResult.ErrorCode);

        var newBookingId = createResult.Value.BookingId;

        var confirmResult = await _confirmBooking.HandleAsync(new ConfirmBookingCommand
        {
            HoldId = newBookingId,
            Notes = "Re-arranged"
        }, ct);

        if (!confirmResult.IsSuccess)
            return Result<ExecuteRearrangementResponse>.Fail(
                confirmResult.StatusCode,
                confirmResult.ErrorMessage ?? "Unable to confirm replacement booking.",
                confirmResult.ErrorCode);

        var cancelResult = await _cancelBooking.HandleAsync(new CancelBookingCommand
        {
            BookingId = hold.Id,
            ReasonCode = string.IsNullOrWhiteSpace(request.ReasonCode) ? "REARRANGED" : request.ReasonCode,
            ReasonDetail = request.ReasonDetail,
            RequestedBy = actorRole
        }, ct);

        if (!cancelResult.IsSuccess)
        {
            _logger.LogError(
                "Rearrangement partially completed. PreviousBookingId={PreviousBookingId} NewBookingId={NewBookingId} CancelError={CancelError}",
                hold.Id,
                newBookingId,
                cancelResult.ErrorMessage);

            return Result<ExecuteRearrangementResponse>.Fail(
                HttpStatusCode.Conflict,
                $"Rearrangement created new booking '{newBookingId}' but failed to cancel previous booking '{hold.Id}'.",
                Errors.Conflict);
        }

        var newSlot = await _slots.GetAsync(request.NewSlotId.Trim(), ct);
        var summary = BuildChangeSummary(oldSlot, newSlot);

        return Result<ExecuteRearrangementResponse>.Ok(new ExecuteRearrangementResponse
        {
            PreviousBookingId = hold.Id,
            NewBookingId = newBookingId,
            NewSlotId = request.NewSlotId.Trim(),
            Status = "Completed",
            ApprovalRequired = actorRole == "Adviser",
            RoutedTo = actorRole == "Adviser" ? _workflowOptions.ApprovalRoutedTo : null,
            ApprovedBy = request.ApprovedBy,
            ChangeSummary = summary,
            NotificationChannels = BuildNotificationChannels()
        });
    }

    private IReadOnlyList<string> BuildConsiderations()
    {
        var considerations = new List<string>
        {
            "Xplan updates and task/thread amendments are flagged for follow-up in this flow.",
            "If advisers are unavailable, users can select an alternative adviser using existing Lead Tech prioritisation.",
            "Outlook 'Show As' misuse can impact availability quality and should be controlled via templates/add-ins.",
            "Duplicate-client handling remains a separate process to be explored."
        };

        if (_workflowOptions.SmsEnabled)
            considerations.Add("SMS/text can be used as an additional communication channel.");

        if (_workflowOptions.HandleEmailBounces)
            considerations.Add("Email bounce-backs should be captured and retried/escalated.");

        return considerations;
    }

    private IReadOnlyList<string> BuildNotificationChannels()
    {
        var channels = new List<string> { "Email" };
        if (_workflowOptions.SmsEnabled)
            channels.Add("SMS");

        return channels;
    }

    private static IReadOnlyList<string> BuildChangeSummary(
        Domain.Transactions.BookingSlot previous,
        Domain.Transactions.BookingSlot? replacement)
    {
        if (replacement is null)
        {
            return
            [
                "Previous meeting was cancelled.",
                "Replacement booking was confirmed."
            ];
        }

        return
        [
            $"Time changed: {previous.StartUtc:O} -> {replacement.StartUtc:O}.",
            $"Adviser changed: {previous.AdviserName} ({previous.AdviserId}) -> {replacement.AdviserName} ({replacement.AdviserId})."
        ];
    }

    private static bool TryParseActorRole(string? value, out string role)
    {
        role = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        role = value.Trim();
        return role.Equals("Client", StringComparison.OrdinalIgnoreCase)
               || role.Equals("Adviser", StringComparison.OrdinalIgnoreCase)
               || role.Equals("LeadTech", StringComparison.OrdinalIgnoreCase)
               || role.Equals("Management", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParsePreferredStartUtc(string? value, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            utc = DateTime.SpecifyKind(dateOnly.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            return true;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            utc = DateTime.SpecifyKind(dto.UtcDateTime, DateTimeKind.Utc);
            return true;
        }

        return false;
    }
}
