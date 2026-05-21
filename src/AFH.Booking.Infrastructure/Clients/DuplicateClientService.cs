using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Models.Clients;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class DuplicateClientService : IDuplicateClientService
{
    private readonly BookingDbContext _db;

    public DuplicateClientService(BookingDbContext db)
    {
        _db = db;
    }

    public async Task<DuplicateClientCaseResponse> CreateCaseAsync(
        string primaryTransactionRef,
        string duplicateTransactionRef,
        string? notes,
        string? raisedBy,
        CancellationToken ct)
    {
        var model = new DuplicateClientCaseModel
        {
            Id = Guid.NewGuid().ToString("N"),
            PrimaryTransactionRef = primaryTransactionRef.Trim(),
            DuplicateTransactionRef = duplicateTransactionRef.Trim(),
            Notes = notes,
            RaisedBy = string.IsNullOrWhiteSpace(raisedBy) ? "System" : raisedBy.Trim(),
            RaisedUtc = DateTime.UtcNow,
            Status = "Pending"
        };

        _db.DuplicateClientCases.Add(model);
        await _db.SaveChangesAsync(ct);

        return ToResponse(model);
    }

    public async Task<IReadOnlyList<DuplicateClientCaseResponse>> ListPendingAsync(CancellationToken ct)
    {
        var rows = await _db.DuplicateClientCases
            .AsNoTracking()
            .Where(x => x.Status == "Pending")
            .OrderByDescending(x => x.RaisedUtc)
            .ToListAsync(ct);

        return rows.Select(ToResponse).ToList();
    }

    public async Task<DuplicateClientCaseResponse?> ResolveCaseAsync(
        string caseId,
        string resolution,
        string? resolvedBy,
        string? notes,
        CancellationToken ct)
    {
        var row = await _db.DuplicateClientCases.SingleOrDefaultAsync(x => x.Id == caseId, ct);
        if (row is null)
            return null;

        row.Status = "Resolved";
        row.Resolution = resolution.Trim();
        row.ResolvedBy = string.IsNullOrWhiteSpace(resolvedBy) ? "System" : resolvedBy.Trim();
        row.ResolvedUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(notes))
            row.Notes = string.IsNullOrWhiteSpace(row.Notes) ? notes.Trim() : $"{row.Notes}\n{notes.Trim()}";

        await _db.SaveChangesAsync(ct);
        return ToResponse(row);
    }

    private static DuplicateClientCaseResponse ToResponse(DuplicateClientCaseModel model)
    {
        return new DuplicateClientCaseResponse
        {
            CaseId = model.Id,
            PrimaryTransactionRef = model.PrimaryTransactionRef,
            DuplicateTransactionRef = model.DuplicateTransactionRef,
            Status = model.Status,
            Notes = model.Notes,
            RaisedBy = model.RaisedBy,
            RaisedUtc = model.RaisedUtc,
            Resolution = model.Resolution,
            ResolvedBy = model.ResolvedBy,
            ResolvedUtc = model.ResolvedUtc
        };
    }
}
