using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Domain.Client;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Mapping;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFH.Booking.Infrastructure.Approvals;

public sealed class DbApprovalWorkflowStore : IApprovalWorkflowStore
{
    private readonly BookingDbContext _db;
    private readonly IClientDirectory _clients;
    private readonly ILogger<DbApprovalWorkflowStore> _logger;

    public DbApprovalWorkflowStore(BookingDbContext db)
        : this(db, NullClientDirectory.Instance, NullLogger<DbApprovalWorkflowStore>.Instance)
    {
    }

    public DbApprovalWorkflowStore(
        BookingDbContext db,
        IClientDirectory clients,
        ILogger<DbApprovalWorkflowStore> logger)
    {
        _db = db;
        _clients = clients;
        _logger = logger;
    }

    public async Task<ApprovalBookingSnapshot> LoadBookingAsync(
        string bookingId,
        CancellationToken ct)
    {
        var lookup = bookingId.Trim();
        var hold = await _db.Holds.AsNoTracking().SingleOrDefaultAsync(x => x.Id == lookup || x.Reference == lookup, ct)
            ?? throw new InvalidOperationException($"Booking '{bookingId}' was not found.");
        var slot = await _db.BookingSlots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == hold.SlotId, ct)
            ?? throw new InvalidOperationException($"Slot '{hold.SlotId}' was not found.");
        var tx = await _db.BookingTransactions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == slot.TransactionId, ct)
            ?? throw new InvalidOperationException($"Transaction '{slot.TransactionId}' was not found.");

        return new ApprovalBookingSnapshot(
            BookingHoldMapping.ToDomain(hold),
            BookingSlotMapping.ToDomain(slot),
            tx.ToDomain(includeSlots: false));
    }

    public Task AddRequestAsync(
        ApprovalWorkflowRecord request,
        ApprovalHistoryRecord history,
        CancellationToken ct)
    {
        _db.ApprovalRequests.Add(ToModel(request));
        _db.ApprovalHistory.Add(ToModel(history));
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ApprovalWorkflowRecord>> ListPendingAsync(CancellationToken ct)
    {
        var rows = await _db.ApprovalRequests
            .AsNoTracking()
            .Where(x => x.Status == "Pending")
            .OrderByDescending(x => x.RequestedUtc)
            .ToListAsync(ct);

        return await EnrichAsync(rows.Select(ToRecord).ToList(), ct);
    }

    public async Task<IReadOnlyList<ApprovalWorkflowRecord>> ListAsync(
        ListApprovalWorkflowRequestsQuery query,
        CancellationToken ct)
    {
        var rows = _db.ApprovalRequests.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.RequesterId))
        {
            var requesterId = query.RequesterId.Trim();
            rows = rows.Where(x => x.RequesterId == requesterId);
        }

        if (query.BookingIds.Count > 0)
        {
            var bookingIds = Normalize(query.BookingIds);
            rows = rows.Where(x => bookingIds.Contains(x.BookingId) || bookingIds.Contains(x.BookingReference!));
        }

        if (query.BookingReferences is { Count: > 0 })
        {
            var bookingReferences = Normalize(query.BookingReferences);
            rows = rows.Where(x =>
                bookingReferences.Contains(x.BookingReference!) ||
                _db.Holds.Any(hold =>
                    (hold.Id == x.BookingId || hold.Reference == x.BookingId || hold.Reference == x.BookingReference) &&
                    bookingReferences.Contains(hold.Reference!)));
        }

        if (query.Statuses.Count > 0)
        {
            var statuses = Normalize(query.Statuses);
            rows = rows.Where(x => statuses.Contains(x.Status));
        }

        if (query.ChangeTypes.Count > 0)
        {
            var changeTypes = Normalize(query.ChangeTypes);
            rows = rows.Where(x => changeTypes.Contains(x.ChangeType));
        }

        if (query.RequestedBys is { Count: > 0 })
        {
            var requestedBys = Normalize(query.RequestedBys);
            rows = rows.Where(x => requestedBys.Contains(x.RequestedBy));
        }

        if (query.TransactionIds is { Count: > 0 })
        {
            var transactionIds = Normalize(query.TransactionIds);
            rows = rows.Where(x => transactionIds.Contains(x.TransactionId));
        }

        if (query.TransactionRefs is { Count: > 0 })
        {
            var transactionRefs = Normalize(query.TransactionRefs);
            rows = rows.Where(x =>
                _db.Holds.Any(hold =>
                    (hold.Id == x.BookingId || hold.Reference == x.BookingId || hold.Reference == x.BookingReference) &&
                    _db.BookingSlots.Any(slot =>
                        slot.Id == hold.SlotId &&
                        _db.BookingTransactions.Any(tx =>
                            tx.Id == slot.TransactionId &&
                            transactionRefs.Contains(tx.TransactionRef)))));
        }

        if (query.AdviserIds is { Count: > 0 })
        {
            var adviserIds = Normalize(query.AdviserIds);
            rows = rows.Where(x =>
                _db.Holds.Any(hold =>
                    (hold.Id == x.BookingId || hold.Reference == x.BookingId || hold.Reference == x.BookingReference) &&
                    _db.BookingSlots.Any(slot =>
                        slot.Id == hold.SlotId &&
                        adviserIds.Contains(slot.AdviserId))));
        }

        if (query.AdviserNames is { Count: > 0 })
        {
            var adviserNames = Normalize(query.AdviserNames);
            rows = rows.Where(x =>
                _db.Holds.Any(hold =>
                    (hold.Id == x.BookingId || hold.Reference == x.BookingId || hold.Reference == x.BookingReference) &&
                    _db.BookingSlots.Any(slot =>
                        slot.Id == hold.SlotId &&
                        adviserNames.Contains(slot.AdviserName))));
        }

        if (query.ClientNames is { Count: > 0 })
        {
            var clientNames = Normalize(query.ClientNames);
            rows = rows.Where(x =>
                _db.Holds.Any(hold =>
                    (hold.Id == x.BookingId || hold.Reference == x.BookingId || hold.Reference == x.BookingReference) &&
                    _db.BookingSlots.Any(slot =>
                        slot.Id == hold.SlotId &&
                        _db.BookingTransactions.Any(tx =>
                            tx.Id == slot.TransactionId &&
                            clientNames.Contains(tx.ClientName!)))));
        }

        if (query.MeetingTypes is { Count: > 0 })
        {
            var meetingTypes = Normalize(query.MeetingTypes);
            rows = rows.Where(x =>
                _db.Holds.Any(hold =>
                    (hold.Id == x.BookingId || hold.Reference == x.BookingId || hold.Reference == x.BookingReference) &&
                    _db.BookingSlots.Any(slot =>
                        slot.Id == hold.SlotId &&
                        _db.BookingTransactions.Any(tx =>
                            tx.Id == slot.TransactionId &&
                            meetingTypes.Contains(tx.MeetingType!)))));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            rows = rows.Where(x =>
                (x.Id != null && x.Id.ToLower().Contains(search)) ||
                (x.Reference != null && x.Reference.ToLower().Contains(search)) ||
                (x.BookingId != null && x.BookingId.ToLower().Contains(search)) ||
                (x.BookingReference != null && x.BookingReference.ToLower().Contains(search)) ||
                (x.TransactionId != null && x.TransactionId.ToLower().Contains(search)) ||
                (x.ChangeType != null && x.ChangeType.ToLower().Contains(search)) ||
                (x.RequestedBy != null && x.RequestedBy.ToLower().Contains(search)) ||
                (x.RequesterId != null && x.RequesterId.ToLower().Contains(search)) ||
                _db.Holds.Any(hold =>
                    (hold.Id == x.BookingId || hold.Reference == x.BookingId || hold.Reference == x.BookingReference) &&
                    ((hold.Reference != null && hold.Reference.ToLower().Contains(search)) ||
                     _db.BookingSlots.Any(slot =>
                         slot.Id == hold.SlotId &&
                         ((slot.AdviserId != null && slot.AdviserId.ToLower().Contains(search)) ||
                          (slot.AdviserName != null && slot.AdviserName.ToLower().Contains(search)) ||
                          _db.BookingTransactions.Any(tx =>
                              tx.Id == slot.TransactionId &&
                              ((tx.TransactionRef != null && tx.TransactionRef.ToLower().Contains(search)) ||
                               (tx.BookingReference != null && tx.BookingReference.ToLower().Contains(search)) ||
                               (tx.ClientName != null && tx.ClientName.ToLower().Contains(search)) ||
                               (tx.ClientEmail != null && tx.ClientEmail.ToLower().Contains(search)) ||
                               (tx.MeetingType != null && tx.MeetingType.ToLower().Contains(search)))))))));
        }

        if (query.FromUtc.HasValue || query.ToUtc.HasValue)
        {
            var filterRequestedDate = string.Equals(query.DateField, "requested", StringComparison.OrdinalIgnoreCase)
                                      || string.Equals(query.DateField, "created", StringComparison.OrdinalIgnoreCase);
            var fromUtc = query.FromUtc.HasValue ? DateTime.SpecifyKind(query.FromUtc.Value, DateTimeKind.Utc) : (DateTime?)null;
            var toUtc = query.ToUtc.HasValue ? DateTime.SpecifyKind(query.ToUtc.Value, DateTimeKind.Utc) : (DateTime?)null;

            rows = filterRequestedDate
                ? ApplyRequestedDateFilter(rows, fromUtc, toUtc)
                : ApplyBookingDateFilter(rows, fromUtc, toUtc);
        }

        var results = await rows
            .OrderByDescending(x => x.RequestedUtc)
            .ToListAsync(ct);

        return await EnrichAsync(results.Select(ToRecord).ToList(), ct);
    }

    public async Task<bool> HasPendingRequestAsync(
        string bookingId,
        string? bookingReference,
        string changeType,
        string requestedBy,
        string? requesterId,
        CancellationToken ct)
    {
        var bookingLookups = Normalize([bookingId, bookingReference ?? string.Empty]);
        if (bookingLookups.Length == 0)
            return false;

        var normalizedChangeType = changeType.Trim();
        var normalizedRequestedBy = requestedBy.Trim();
        var normalizedRequesterId = requesterId?.Trim();

        var rows = _db.ApprovalRequests
            .AsNoTracking()
            .Where(x =>
                x.Status == "Pending" &&
                (bookingLookups.Contains(x.BookingId) || bookingLookups.Contains(x.BookingReference!)) &&
                x.ChangeType == normalizedChangeType &&
                x.RequestedBy == normalizedRequestedBy);

        if (!string.IsNullOrWhiteSpace(normalizedRequesterId))
        {
            rows = rows.Where(x => x.RequesterId == normalizedRequesterId);
        }

        return await rows.AnyAsync(ct);
    }

    public async Task<ApprovalWorkflowRecord?> GetPendingRequestAsync(
        string bookingId,
        string? bookingReference,
        string changeType,
        string requestedBy,
        string? requesterId,
        CancellationToken ct)
    {
        var bookingLookups = Normalize([bookingId, bookingReference ?? string.Empty]);
        if (bookingLookups.Length == 0)
            return null;

        var normalizedChangeType = changeType.Trim();
        var normalizedRequestedBy = requestedBy.Trim();
        var normalizedRequesterId = requesterId?.Trim();

        var rows = _db.ApprovalRequests
            .AsNoTracking()
            .Where(x =>
                x.Status == "Pending" &&
                (bookingLookups.Contains(x.BookingId) || bookingLookups.Contains(x.BookingReference!)) &&
                x.ChangeType == normalizedChangeType &&
                x.RequestedBy == normalizedRequestedBy);

        if (!string.IsNullOrWhiteSpace(normalizedRequesterId))
        {
            rows = rows.Where(x => x.RequesterId == normalizedRequesterId);
        }

        var row = await rows
            .OrderByDescending(x => x.RequestedUtc)
            .FirstOrDefaultAsync(ct);

        if (row is null)
            return null;

        var enriched = await EnrichAsync([ToRecord(row)], ct);
        return enriched.FirstOrDefault();
    }

    public async Task<ApprovalWorkflowRecord?> GetPendingRearrangeRequestForNewSlotAsync(
        string newSlotId,
        string requestedBy,
        string? requesterId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(newSlotId))
            return null;

        var normalizedSlotId = newSlotId.Trim();
        var normalizedRequestedBy = requestedBy.Trim();
        var normalizedRequesterId = requesterId?.Trim();
        var newSlotNeedle = $"%\"newSlotId\":\"{EscapeLike(normalizedSlotId)}\"%";
        var alternativeSlotNeedle = $"%\"slotId\":\"{EscapeLike(normalizedSlotId)}\"%";

        var rows = _db.ApprovalRequests
            .AsNoTracking()
            .Where(x =>
                x.Status == "Pending" &&
                x.ChangeType == "Rearrange" &&
                x.RequestedBy == normalizedRequestedBy &&
                x.RequestedPayloadJson != null &&
                (EF.Functions.Like(x.RequestedPayloadJson, newSlotNeedle) ||
                 EF.Functions.Like(x.RequestedPayloadJson, alternativeSlotNeedle)));

        if (!string.IsNullOrWhiteSpace(normalizedRequesterId))
        {
            rows = rows.Where(x => x.RequesterId == normalizedRequesterId);
        }

        var row = await rows
            .OrderByDescending(x => x.RequestedUtc)
            .FirstOrDefaultAsync(ct);

        if (row is null)
            return null;

        var enriched = await EnrichAsync([ToRecord(row)], ct);
        return enriched.FirstOrDefault();
    }

    private static string[] Normalize(IReadOnlyList<string> values)
        => values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private IQueryable<ApprovalRequestModel> ApplyRequestedDateFilter(
        IQueryable<ApprovalRequestModel> rows,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        if (fromUtc.HasValue)
            rows = rows.Where(x => x.RequestedUtc >= fromUtc.Value);

        if (toUtc.HasValue)
            rows = rows.Where(x => x.RequestedUtc <= toUtc.Value);

        return rows;
    }

    private IQueryable<ApprovalRequestModel> ApplyBookingDateFilter(
        IQueryable<ApprovalRequestModel> rows,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        if (fromUtc.HasValue)
        {
            rows = rows.Where(x =>
                _db.Holds.Any(hold =>
                    (hold.Id == x.BookingId || hold.Reference == x.BookingId || hold.Reference == x.BookingReference) &&
                    _db.BookingSlots.Any(slot =>
                        slot.Id == hold.SlotId &&
                        slot.StartUtc >= fromUtc.Value)));
        }

        if (toUtc.HasValue)
        {
            rows = rows.Where(x =>
                _db.Holds.Any(hold =>
                    (hold.Id == x.BookingId || hold.Reference == x.BookingId || hold.Reference == x.BookingReference) &&
                    _db.BookingSlots.Any(slot =>
                        slot.Id == hold.SlotId &&
                        slot.StartUtc <= toUtc.Value)));
        }

        return rows;
    }

    public async Task<ApprovalWorkflowRecord?> GetAsync(string requestId, CancellationToken ct)
    {
        var lookup = requestId.Trim();
        var row = await _db.ApprovalRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == lookup || x.Reference == lookup, ct);
        if (row is null)
            return null;

        var enriched = await EnrichAsync([ToRecord(row)], ct);
        return enriched.FirstOrDefault();
    }

    public async Task<ApprovalWorkflowRecord?> GetForUpdateAsync(string requestId, CancellationToken ct)
    {
        var lookup = requestId.Trim();
        var row = await _db.ApprovalRequests.SingleOrDefaultAsync(x => x.Id == lookup || x.Reference == lookup, ct);
        if (row is null)
            return null;

        var enriched = await EnrichAsync([ToRecord(row)], ct);
        return enriched.FirstOrDefault();
    }

    public async Task UpdateAsync(ApprovalWorkflowRecord request, CancellationToken ct)
    {
        var row = await _db.ApprovalRequests.SingleAsync(x => x.Id == request.Id, ct);
        Apply(request, row);
    }

    public Task AddHistoryAsync(ApprovalHistoryRecord history, CancellationToken ct)
    {
        _db.ApprovalHistory.Add(ToModel(history));
        return Task.CompletedTask;
    }

    public async Task<bool> IsApprovedAsync(
        string requestId,
        string bookingId,
        string changeType,
        string requestedBy,
        CancellationToken ct)
    {
        var requestLookup = requestId.Trim();
        var bookingLookup = bookingId.Trim();
        return await _db.ApprovalRequests
            .AsNoTracking()
            .AnyAsync(
                x => (x.Id == requestLookup || x.Reference == requestLookup) &&
                     (x.BookingId == bookingLookup || x.BookingReference == bookingLookup) &&
                     x.ChangeType == changeType &&
                     x.RequestedBy == requestedBy &&
                     x.Status == "Approved",
                ct);
    }

    private static ApprovalWorkflowRecord ToRecord(ApprovalRequestModel model)
    {
        return new ApprovalWorkflowRecord
        {
            Id = model.Id,
            Reference = model.Reference,
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
            RequestedPayloadJson = model.RequestedPayloadJson,
            ApproverTargetType = model.ApproverTargetType,
            ApproverTargetValue = model.ApproverTargetValue,
            ApproverTargetDisplayName = model.ApproverTargetDisplayName,
            Reviewer = model.Reviewer,
            ReviewedUtc = model.ReviewedUtc,
            ReviewNotes = model.ReviewNotes,
            ExecutedUtc = model.ExecutedUtc,
            ExecutionError = model.ExecutionError
        };
    }

    private async Task<IReadOnlyList<ApprovalWorkflowRecord>> EnrichAsync(
        IReadOnlyList<ApprovalWorkflowRecord> records,
        CancellationToken ct)
    {
        if (records.Count == 0)
        {
            return records;
        }

        var bookingIds = records
            .Select(record => record.BookingId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var holds = await _db.Holds
            .AsNoTracking()
            .Where(hold => bookingIds.Contains(hold.Id) || bookingIds.Contains(hold.Reference!))
            .ToListAsync(ct);

        var slotIds = holds
            .Select(hold => hold.SlotId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var slots = await _db.BookingSlots
            .AsNoTracking()
            .Where(slot => slotIds.Contains(slot.Id))
            .ToDictionaryAsync(slot => slot.Id, StringComparer.OrdinalIgnoreCase, ct);

        var transactionIds = slots.Values
            .Select(slot => slot.TransactionId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var transactions = await _db.BookingTransactions
            .AsNoTracking()
            .Where(tx => transactionIds.Contains(tx.Id))
            .Select(tx => new ApprovalTransactionEnrichment(
                tx.Id,
                tx.TransactionRef,
                tx.BookingReference,
                tx.ClientName,
                tx.MeetingType))
            .ToDictionaryAsync(tx => tx.Id, StringComparer.OrdinalIgnoreCase, ct);

        var clients = await LoadClientsAsync(transactions.Values, ct);

        var holdsByLookup = holds
            .SelectMany(hold => new[] { hold.Id, hold.Reference }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => new { Key = value!, Hold = hold }))
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Hold, StringComparer.OrdinalIgnoreCase);

        var clientNameSyncs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in records)
        {
            if (!holdsByLookup.TryGetValue(record.BookingId, out var hold) ||
                !slots.TryGetValue(hold.SlotId, out var slot) ||
                !transactions.TryGetValue(slot.TransactionId, out var tx))
            {
                continue;
            }

            var directoryClientName = BuildClientName(GetClient(clients, tx.TransactionRef));
            record.BookingReference = FirstNonBlank(record.BookingReference, tx.BookingReference, hold.Reference);
            record.ClientName = FirstNonBlank(record.ClientName, tx.ClientName, directoryClientName);
            record.AdviserName = FirstNonBlank(record.AdviserName, slot.AdviserName);
            record.BookingDateTime ??= DateTime.SpecifyKind(slot.StartUtc, DateTimeKind.Utc);
            record.MeetingType = FirstNonBlank(record.MeetingType, tx.MeetingType);

            if (string.IsNullOrWhiteSpace(tx.ClientName) && !string.IsNullOrWhiteSpace(directoryClientName))
            {
                clientNameSyncs[tx.Id] = directoryClientName;
            }
        }

        await SyncMissingTransactionClientNamesAsync(clientNameSyncs, ct);

        return records;
    }

    private async Task SyncMissingTransactionClientNamesAsync(
        IReadOnlyDictionary<string, string> clientNameSyncs,
        CancellationToken ct)
    {
        if (clientNameSyncs.Count == 0)
            return;

        var transactionIds = clientNameSyncs.Keys.ToArray();
        var transactions = await _db.BookingTransactions
            .Where(tx => transactionIds.Contains(tx.Id))
            .ToListAsync(ct);

        foreach (var transaction in transactions)
        {
            if (!string.IsNullOrWhiteSpace(transaction.ClientName) ||
                !clientNameSyncs.TryGetValue(transaction.Id, out var clientName))
            {
                continue;
            }

            transaction.ClientName = Limit(clientName, 256);
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyDictionary<string, ClientDirectoryItem?>> LoadClientsAsync(
        IEnumerable<ApprovalTransactionEnrichment> transactions,
        CancellationToken ct)
    {
        var cache = new Dictionary<string, ClientDirectoryItem?>(StringComparer.OrdinalIgnoreCase);
        var lookupRefs = transactions
            .Select(tx => tx.TransactionRef)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var lookupRef in lookupRefs)
        {
            cache[lookupRef] = await TryGetClientAsync(lookupRef, ct);
        }

        return cache;
    }

    private async Task<ClientDirectoryItem?> TryGetClientAsync(string transactionRef, CancellationToken ct)
    {
        try
        {
            return await _clients.GetAsync(transactionRef, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Client lookup skipped while enriching approval requests. TransactionRef={TransactionRef}", transactionRef);
            return null;
        }
    }

    private static ClientDirectoryItem? GetClient(
        IReadOnlyDictionary<string, ClientDirectoryItem?> clients,
        string? transactionRef)
    {
        return !string.IsNullOrWhiteSpace(transactionRef) && clients.TryGetValue(transactionRef.Trim(), out var client)
            ? client
            : null;
    }

    private static string? BuildClientName(ClientDirectoryItem? client)
    {
        if (client is null)
            return null;

        var first = string.IsNullOrWhiteSpace(client.FirstName) ? null : client.FirstName.Trim();
        var last = string.IsNullOrWhiteSpace(client.LastName) ? null : client.LastName.Trim();
        var value = string.Join(" ", new[] { first, last }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string Limit(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string EscapeLike(string value)
        => value
            .Replace("[", "[[]", StringComparison.Ordinal)
            .Replace("%", "[%]", StringComparison.Ordinal)
            .Replace("_", "[_]", StringComparison.Ordinal);

    private static ApprovalRequestModel ToModel(ApprovalWorkflowRecord record)
    {
        var model = new ApprovalRequestModel();
        Apply(record, model);
        return model;
    }

    private static void Apply(ApprovalWorkflowRecord source, ApprovalRequestModel target)
    {
        target.Id = source.Id;
        target.Reference = source.Reference;
        target.BookingId = source.BookingId;
        target.BookingReference = source.BookingReference;
        target.TransactionId = source.TransactionId;
        target.ChangeType = source.ChangeType;
        target.RequestedBy = source.RequestedBy;
        target.RequesterId = source.RequesterId;
        target.Status = source.Status;
        target.RequestedUtc = source.RequestedUtc;
        target.ReasonCode = source.ReasonCode;
        target.ReasonDetail = source.ReasonDetail;
        target.RequestedPayloadJson = source.RequestedPayloadJson;
        target.ApproverTargetType = source.ApproverTargetType ?? string.Empty;
        target.ApproverTargetValue = source.ApproverTargetValue ?? string.Empty;
        target.ApproverTargetDisplayName = source.ApproverTargetDisplayName ?? string.Empty;
        target.Reviewer = source.Reviewer;
        target.ReviewedUtc = source.ReviewedUtc;
        target.ReviewNotes = source.ReviewNotes;
        target.ExecutedUtc = source.ExecutedUtc;
        target.ExecutionError = source.ExecutionError;
    }

    private static ApprovalHistoryModel ToModel(ApprovalHistoryRecord record)
    {
        return new ApprovalHistoryModel
        {
            Id = record.Id,
            ApprovalRequestId = record.ApprovalRequestId,
            EventType = record.EventType,
            ActorType = record.ActorType,
            ActorId = record.ActorId,
            Outcome = record.Outcome,
            Comments = record.Comments,
            OccurredUtc = record.OccurredUtc
        };
    }

    private sealed record ApprovalTransactionEnrichment(
        string Id,
        string? TransactionRef,
        string? BookingReference,
        string? ClientName,
        string? MeetingType);

    private sealed class NullClientDirectory : IClientDirectory
    {
        public static readonly NullClientDirectory Instance = new();

        public Task<ClientDirectoryItem?> GetAsync(string transactionIdOrClientId, CancellationToken ct)
            => Task.FromResult<ClientDirectoryItem?>(null);
    }
}
