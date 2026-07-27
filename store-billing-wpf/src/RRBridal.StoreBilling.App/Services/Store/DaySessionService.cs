using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Api;
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
    private CentralOnlineModeService? _centralMode;
    private CentralStorePosClient? _storePos;
    private CentralDashboardClient? _dashboardApi;

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

    public void ConfigureOnline(CentralOnlineModeService centralMode, CentralStorePosClient storePos, CentralDashboardClient? dashboardApi)
    {
        _centralMode = centralMode;
        _storePos = storePos;
        _dashboardApi = dashboardApi;
        if (dashboardApi != null)
            _dayClose.ConfigureOnline(centralMode, dashboardApi, storePos);
    }

    public static string FormatBusinessDate(DateTime localDate) =>
        localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public async Task<DaySessionRecord?> GetSessionAsync(
        string storeId,
        string businessDate,
        string posCounter,
        CancellationToken ct = default)
    {
        if (_centralMode?.IsOnlineMode == true && _storePos != null)
        {
            using var json = await _storePos.GetDaySessionAsync(businessDate, ct);
            if (json.RootElement.ValueKind == JsonValueKind.Null
                || json.RootElement.ValueKind == JsonValueKind.Undefined)
                return null;
            if (json.RootElement.ValueKind != JsonValueKind.Object)
                return null;
            if (!json.RootElement.TryGetProperty("payload", out var payload)
                || payload.ValueKind != JsonValueKind.Object)
                return null;
            var doc = BsonDocument.Parse(payload.GetRawText());
            return DaySessionDocumentMapper.ToRecord(doc);
        }

        var filter = SessionFilter(storeId, businessDate, posCounter);
        var local = await _sessions.Find(filter).FirstOrDefaultAsync(ct);
        return DaySessionDocumentMapper.ToRecord(local);
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
            if (_centralMode?.IsOnlineMode != true)
                await _sessions.InsertOneAsync(doc, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            var again = await GetSessionAsync(storeId, businessDate, posCounter, ct);
            return (false, "Day session already exists for this counter.", again);
        }

        await _outbox.PublishDaySessionOpenedAsync(doc, ct);

        // Online: no local Mongo domain writes (including audit). Offline keeps local audit trail.
        if (_centralMode?.IsOnlineMode != true && _auditLog != null)
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

        decimal expectedCash;
        DayBillingCloseSnapshot? snapshot = null;
        if (_centralMode?.IsOnlineMode == true)
        {
            if (_dashboardApi == null)
                return (false, "Central dashboard is not configured for online day close.", session);

            // Same gate as Offline: pending stock exceptions must be approved on central first.
            snapshot = await _dayClose.LoadDayCloseAsync(storeId, date, posCounter, session, ct);
            if (snapshot.StockExceptions.Any(e => e.CanApprove))
                return (false, "Approve or resolve stock exceptions before closing the day.", session);

            // Prefer computed expected cash from the day-close report (payload expectedCash is often 0 while open).
            using var reportJson = await _dashboardApi.GetStoreDayCloseReportAsync(businessDate, posCounter, ct);
            expectedCash = StoreDayCloseDashboardReader.ReadReportExpectedCash(reportJson.RootElement);
            if (expectedCash <= 0 && snapshot.ExpectedCash > 0)
                expectedCash = snapshot.ExpectedCash;
        }
        else
        {
            snapshot = await _dayClose.LoadDayCloseAsync(storeId, date, posCounter, session, ct);
            if (snapshot.StockExceptions.Any(e => e.CanApprove))
                return (false, "Approve or resolve stock exceptions before closing the day.", session);
            expectedCash = snapshot.ExpectedCash;
        }

        var cashDifference = actualCashCounted - expectedCash;
        var closedAtUtc = DateTime.UtcNow.ToString("O");

        if (_centralMode?.IsOnlineMode == true)
        {
            var closedDoc = new BsonDocument
            {
                { "sessionId", session.SessionId },
                { "storeId", storeId },
                { "posCounter", posCounter },
                { "deviceId", _storeContext.DeviceId },
                { "businessDate", businessDate },
                { "status", DaySessionStatus.Closed },
                { "openingCash", (double)session.OpeningCash },
                { "expectedCash", (double)expectedCash },
                { "actualCashCounted", (double)actualCashCounted },
                { "cashDifference", (double)cashDifference },
                { "cashDenominations", DaySessionDocumentMapper.ToDenominationArray(denominations) },
                { "closedBy", closedBy.Trim() },
                { "closedAtUtc", closedAtUtc },
                { "notes", string.IsNullOrWhiteSpace(notes) ? (BsonValue)BsonNull.Value : notes.Trim() },
                { "cashTaken", string.IsNullOrWhiteSpace(cashTaken) ? (BsonValue)BsonNull.Value : cashTaken.Trim() },
            };
            await _outbox.PublishDaySessionClosedAsync(closedDoc, ct);
            return (true, "Day closed on central.", DaySessionDocumentMapper.ToRecord(closedDoc));
        }

        var update = Builders<BsonDocument>.Update
            .Set("status", DaySessionStatus.Closed)
            .Set("expectedCash", (double)expectedCash)
            .Set("actualCashCounted", (double)actualCashCounted)
            .Set("cashDifference", (double)cashDifference)
            .Set("cashDenominations", DaySessionDocumentMapper.ToDenominationArray(denominations))
            .Set("closeSnapshot", DaySessionDocumentMapper.ToCloseSnapshotDocument(snapshot!))
            .Set("closedBy", closedBy.Trim())
            .Set("closedAtUtc", closedAtUtc)
            .Set("notes", string.IsNullOrWhiteSpace(notes) ? (BsonValue)BsonNull.Value : notes.Trim())
            .Set("cashTaken", string.IsNullOrWhiteSpace(cashTaken) ? (BsonValue)BsonNull.Value : cashTaken.Trim());

        await _sessions.UpdateOneAsync(SessionFilter(storeId, businessDate, posCounter), update, cancellationToken: ct);

        var closedLocal = await _sessions.Find(SessionFilter(storeId, businessDate, posCounter)).FirstAsync(ct);
        await _outbox.PublishDaySessionClosedAsync(closedLocal, ct);

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

        return (true, "Day closed successfully.", DaySessionDocumentMapper.ToRecord(closedLocal));
    }

    public async Task MarkCashHandOverPrintedAsync(
        string storeId,
        string businessDate,
        string posCounter,
        CancellationToken ct = default)
    {
        if (_centralMode?.IsOnlineMode == true)
        {
            if (_storePos == null)
                throw new InvalidOperationException("Central store-pos is not configured for online mode.");

            await _storePos.MarkCashHandOverPrintedAsync(new
            {
                businessDate = businessDate.Trim(),
                posCounter = posCounter.Trim(),
                cashHandOverPrintedAtUtc = DateTime.UtcNow.ToString("O"),
            }, ct);
            return;
        }

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
        if (_centralMode?.IsOnlineMode == true)
        {
            if (_dashboardApi == null)
                throw new InvalidOperationException("Central dashboard is not configured for online mode.");

            using var doc = await _dashboardApi.GetStoreDayCloseAsync(businessDate, null, ct);
            var rows = StoreDayCloseDashboardReader.ReadCounters(doc.RootElement)
                .OrderBy(c => c.PosCounter, StringComparer.OrdinalIgnoreCase)
                .Select(c => new DaySessionRollupRow
                {
                    PosCounter = c.PosCounter ?? "",
                    Status = c.Status ?? "",
                    OpeningCash = c.OpeningCash,
                    ExpectedCash = c.ExpectedCash,
                    ActualCashCounted = c.ActualCashCounted,
                    CashDifference = c.CashDifference,
                    ClosedBy = c.ClosedBy,
                    ClosedAtUtc = c.ClosedAtUtc,
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

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId),
            Builders<BsonDocument>.Filter.Eq("businessDate", businessDate));

        var docs = await _sessions.Find(filter).ToListAsync(ct);
        var localRows = docs
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
            Counters = localRows,
            TotalOpeningCash = localRows.Sum(r => r.OpeningCash),
            TotalExpectedCash = localRows.Sum(r => r.ExpectedCash),
            TotalActualCashCounted = localRows.Sum(r => r.ActualCashCounted),
            TotalCashDifference = localRows.Sum(r => r.CashDifference),
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

    public Task<(bool Success, string Message)> ApproveStockExceptionsAsync(
        string storeId,
        string billNo,
        string approvedByUser,
        CancellationToken ct = default) =>
        _dayClose.ApproveStockExceptionsAsync(storeId, billNo, approvedByUser, ct);

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
