using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class CashMovementService
{
    private readonly IMongoCollection<BsonDocument> _movements;
    private readonly BillNumberGenerator _billNumbers;
    private readonly BillingOutboxPublisher _outbox;
    private readonly StoreContext _storeContext;
    private readonly DaySessionGuard _guard;

    public CashMovementService(
        IMongoDatabase localDb,
        BillNumberGenerator billNumbers,
        BillingOutboxPublisher outbox,
        StoreContext storeContext,
        DaySessionService daySessions)
    {
        _movements = localDb.GetCollection<BsonDocument>("store_cash_movements");
        _billNumbers = billNumbers;
        _outbox = outbox;
        _storeContext = storeContext;
        _guard = new DaySessionGuard(daySessions);
    }

    public async Task<IReadOnlyList<CashMovementRecord>> ListForBusinessDateAsync(
        string storeId,
        string businessDate,
        string? posCounterFilter = null,
        CancellationToken ct = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId),
            Builders<BsonDocument>.Filter.Eq("businessDate", businessDate));

        var docs = await _movements.Find(filter).ToListAsync(ct);
        return docs
            .Where(d => DayBillingCloseDocumentReader.MatchesPosCounterFilter(d, posCounterFilter))
            .OrderByDescending(d => DayBillingCloseDocumentReader.ReadString(d, "createdAtUtc"))
            .Select(DaySessionDocumentMapper.ToMovementRecord)
            .ToList();
    }

    public async Task<(bool Success, string Message)> PostMovementAsync(
        string movementType,
        string description,
        decimal amount,
        string businessDate,
        CancellationToken ct = default)
    {
        if (amount <= 0)
            return (false, "Amount must be greater than zero.");

        if (!string.Equals(movementType, CashMovementType.DepositToBank, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(movementType, CashMovementType.CashWithdrawal, StringComparison.OrdinalIgnoreCase))
            return (false, "Invalid movement type.");

        if (!Regex.IsMatch(businessDate, @"^\d{4}-\d{2}-\d{2}$"))
            return (false, "Business date must be YYYY-MM-DD.");

        var guardMsg = await _guard.ValidatePostingAsync(_storeContext.StoreId, businessDate, _storeContext.PosCounter, ct);
        if (guardMsg != null)
            return (false, guardMsg);

        var movementNo = await _billNumbers.NextCashMovementAsync(ct);
        var createdAtUtc = DateTime.UtcNow.ToString("O");
        var doc = new BsonDocument
        {
            { "movementNo", movementNo },
            { "movementType", movementType },
            { "description", description.Trim() },
            { "amount", (double)amount },
            { "businessDate", businessDate },
            { "status", "posted" },
            { "storeId", _storeContext.StoreId },
            { "posCounter", _storeContext.PosCounter },
            { "deviceId", _storeContext.DeviceId },
            { "createdAtUtc", createdAtUtc },
        };

        await _movements.InsertOneAsync(doc, cancellationToken: ct);
        try { await _outbox.PublishCashMovementCreatedAsync(doc, ct); } catch { /* best-effort */ }

        return (true, $"{movementNo} posted.");
    }
}
