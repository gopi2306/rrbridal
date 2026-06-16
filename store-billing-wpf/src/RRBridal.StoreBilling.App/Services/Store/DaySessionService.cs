using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Audit;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Products;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class DaySessionService
{
    private readonly IMongoCollection<BsonDocument> _sessions;
    private readonly DayBillingCloseService _dayClose;
    private readonly BillingOutboxPublisher _outbox;
    private readonly StoreContext _storeContext;
    private readonly StoreAuditLogService? _auditLog;

    public DaySessionService(
        IMongoDatabase localDb,
        ProductCatalogService productCatalog,
        BillingOutboxPublisher outbox,
        StoreContext storeContext,
        StoreAuditLogService? auditLog = null)
    {
        _sessions = localDb.GetCollection<BsonDocument>("store_day_sessions");
        _dayClose = new DayBillingCloseService(localDb, productCatalog, auditLog);
        _outbox = outbox;
        _storeContext = storeContext;
        _auditLog = auditLog;
    }

    public static string FormatBusinessDate(DateTime localDate) =>
        localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public async Task<DaySessionRecord?> GetSessionAsync(
        string storeId,
        string businessDate,
        string posCounter,
        CancellationToken ct = default)
    {
        var filter = SessionFilter(storeId, businessDate, posCounter);
        var doc = await _sessions.Find(filter).FirstOrDefaultAsync(ct);
        return DaySessionDocumentMapper.ToRecord(doc);
    }

    public Task<bool> IsDayOpenAsync(string storeId, string businessDate, string posCounter, CancellationToken ct = default) =>
        HasStatusAsync(storeId, businessDate, posCounter, DaySessionStatus.Open, ct);

    public Task<bool> IsDayLockedAsync(string storeId, string businessDate, string posCounter, CancellationToken ct = default) =>
        HasStatusAsync(storeId, businessDate, posCounter, DaySessionStatus.Closed, ct);

    public async Task<(bool Success, string Message, DaySessionRecord? Session)> OpenDayAsync(
        decimal openingCash,
        string openedBy,
        DateTime? localDate = null,
        CancellationToken ct = default)
    {
        if (openingCash < 0)
            return (false, "Opening cash cannot be negative.", null);

        var storeId = _storeContext.StoreId;
        var posCounter = _storeContext.PosCounter;
        var businessDate = FormatBusinessDate(localDate ?? DateTime.Today);
        var existing = await GetSessionAsync(storeId, businessDate, posCounter, ct);
        if (existing != null)
        {
            if (string.Equals(existing.Status, DaySessionStatus.Open, StringComparison.OrdinalIgnoreCase))
                return (false, "Day is already open for this counter.", existing);
            return (false, "This business day is already closed.", existing);
        }

        var sessionId = Guid.NewGuid().ToString();
        var openedAtUtc = DateTime.UtcNow.ToString("O");
        var doc = new BsonDocument
        {
            { "sessionId", sessionId },
            { "storeId", storeId },
            { "posCounter", posCounter },
            { "deviceId", _storeContext.DeviceId },
            { "businessDate", businessDate },
            { "status", DaySessionStatus.Open },
            { "openingCash", (double)openingCash },
            { "expectedCash", 0d },
            { "actualCashCounted", 0d },
            { "cashDifference", 0d },
            { "openedBy", openedBy.Trim() },
            { "openedAtUtc", openedAtUtc },
        };

        try
        {
            await _sessions.InsertOneAsync(doc, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            var again = await GetSessionAsync(storeId, businessDate, posCounter, ct);
            return (false, "Day session already exists for this counter.", again);
        }

        try { await _outbox.PublishDaySessionOpenedAsync(doc, ct); } catch { /* best-effort */ }

        if (_auditLog != null)
        {
            await _auditLog.LogEventAsync(new StoreAuditEvent
            {
                EntityType = "day_session",
                EntityId = sessionId,
                Action = "day_opened",
                ActorName = openedBy.Trim(),
                Metadata = new BsonDocument
                {
                    { "businessDate", businessDate },
                    { "openingCash", (double)openingCash },
                },
            }, ct);
        }

        return (true, "Day opened.", DaySessionDocumentMapper.ToRecord(doc));
    }

    public async Task<(bool Success, string Message, DaySessionRecord? Session)> CloseDayAsync(
        IReadOnlyList<CashDenominationLine> denominations,
        decimal actualCashCounted,
        string closedBy,
        string? notes,
        string? cashTaken,
        DateTime? localDate = null,
        CancellationToken ct = default)
    {
        if (actualCashCounted <= 0)
            return (false, "Enter a physical cash count before closing.", null);

        if (!CashDenominationDefaults.ValidateDenominations(denominations, actualCashCounted, out var denError))
            return (false, denError ?? "Invalid denomination count.", null);

        var storeId = _storeContext.StoreId;
        var posCounter = _storeContext.PosCounter;
        var date = (localDate ?? DateTime.Today).Date;
        var businessDate = FormatBusinessDate(date);
        var session = await GetSessionAsync(storeId, businessDate, posCounter, ct);
        if (session == null)
            return (false, "Open the day before closing.", null);
        if (string.Equals(session.Status, DaySessionStatus.Closed, StringComparison.OrdinalIgnoreCase))
            return (false, "Day is already closed.", session);

        var snapshot = await _dayClose.LoadDayCloseAsync(storeId, date, posCounter, session, ct);
        if (snapshot.StockExceptions.Any(e => e.CanApprove))
            return (false, "Approve or resolve stock exceptions before closing the day.", session);

        var expectedCash = snapshot.ExpectedCash;
        var cashDifference = actualCashCounted - expectedCash;
        var closedAtUtc = DateTime.UtcNow.ToString("O");

        var update = Builders<BsonDocument>.Update
            .Set("status", DaySessionStatus.Closed)
            .Set("expectedCash", (double)expectedCash)
            .Set("actualCashCounted", (double)actualCashCounted)
            .Set("cashDifference", (double)cashDifference)
            .Set("cashDenominations", DaySessionDocumentMapper.ToDenominationArray(denominations))
            .Set("closeSnapshot", DaySessionDocumentMapper.ToCloseSnapshotDocument(snapshot))
            .Set("closedBy", closedBy.Trim())
            .Set("closedAtUtc", closedAtUtc)
            .Set("notes", string.IsNullOrWhiteSpace(notes) ? (BsonValue)BsonNull.Value : notes.Trim())
            .Set("cashTaken", string.IsNullOrWhiteSpace(cashTaken) ? (BsonValue)BsonNull.Value : cashTaken.Trim());

        await _sessions.UpdateOneAsync(SessionFilter(storeId, businessDate, posCounter), update, cancellationToken: ct);

        var closedDoc = await _sessions.Find(SessionFilter(storeId, businessDate, posCounter)).FirstAsync(ct);
        try { await _outbox.PublishDaySessionClosedAsync(closedDoc, ct); } catch { /* best-effort */ }

        if (_auditLog != null)
        {
            await _auditLog.LogEventAsync(new StoreAuditEvent
            {
                EntityType = "day_session",
                EntityId = session.SessionId,
                Action = "day_closed",
                ActorName = closedBy.Trim(),
                Metadata = new BsonDocument
                {
                    { "businessDate", businessDate },
                    { "expectedCash", (double)expectedCash },
                    { "actualCashCounted", (double)actualCashCounted },
                    { "cashDifference", (double)cashDifference },
                },
            }, ct);
        }

        return (true, "Day closed successfully.", DaySessionDocumentMapper.ToRecord(closedDoc));
    }

    public async Task MarkCashHandOverPrintedAsync(
        string storeId,
        string businessDate,
        string posCounter,
        CancellationToken ct = default)
    {
        var printedAt = DateTime.UtcNow.ToString("O");
        await _sessions.UpdateOneAsync(
            SessionFilter(storeId, businessDate, posCounter),
            Builders<BsonDocument>.Update.Set("cashHandOverPrintedAtUtc", printedAt),
            cancellationToken: ct);
    }

    public async Task<StoreDaySessionRollup> GetStoreRollupAsync(
        string storeId,
        string businessDate,
        CancellationToken ct = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId),
            Builders<BsonDocument>.Filter.Eq("businessDate", businessDate));

        var docs = await _sessions.Find(filter).ToListAsync(ct);
        var rows = docs
            .Select(d => DaySessionDocumentMapper.ToRecord(d)!)
            .OrderBy(s => s.PosCounter, StringComparer.OrdinalIgnoreCase)
            .Select(s => new DaySessionRollupRow
            {
                PosCounter = s.PosCounter,
                Status = s.Status,
                OpeningCash = s.OpeningCash,
                ExpectedCash = s.ExpectedCash,
                ActualCashCounted = s.ActualCashCounted,
                CashDifference = s.CashDifference,
                ClosedBy = s.ClosedBy,
                ClosedAtUtc = s.ClosedAtUtc,
            })
            .ToList();

        return new StoreDaySessionRollup
        {
            BusinessDate = businessDate,
            Counters = rows,
            TotalOpeningCash = rows.Sum(r => r.OpeningCash),
            TotalExpectedCash = rows.Sum(r => r.ExpectedCash),
            TotalActualCashCounted = rows.Sum(r => r.ActualCashCounted),
            TotalCashDifference = rows.Sum(r => r.CashDifference),
        };
    }

    public async Task<DayBillingCloseSnapshot> LoadDayCloseWithSessionAsync(
        string storeId,
        DateTime localDate,
        string? posCounterFilter,
        CancellationToken ct = default)
    {
        DaySessionRecord? session = null;
        if (!string.IsNullOrWhiteSpace(posCounterFilter))
        {
            var businessDate = FormatBusinessDate(localDate);
            session = await GetSessionAsync(storeId, businessDate, posCounterFilter, ct);
        }

        return await _dayClose.LoadDayCloseAsync(storeId, localDate, posCounterFilter, session, ct);
    }

    private async Task<bool> HasStatusAsync(
        string storeId,
        string businessDate,
        string posCounter,
        string status,
        CancellationToken ct)
    {
        var session = await GetSessionAsync(storeId, businessDate, posCounter, ct);
        return session != null
               && string.Equals(session.Status, status, StringComparison.OrdinalIgnoreCase);
    }

    private static FilterDefinition<BsonDocument> SessionFilter(string storeId, string businessDate, string posCounter) =>
        Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId),
            Builders<BsonDocument>.Filter.Eq("businessDate", businessDate),
            Builders<BsonDocument>.Filter.Eq("posCounter", posCounter));
}
