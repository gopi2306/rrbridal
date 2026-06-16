using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class DayCloseReportService
{
    private readonly IMongoDatabase _db;
    private readonly DaySessionService _daySessions;
    private readonly StoreBillListService _billList;

    public DayCloseReportService(
        IMongoDatabase localDb,
        DaySessionService daySessions,
        StoreBillListService billList)
    {
        _db = localDb;
        _daySessions = daySessions;
        _billList = billList;
    }

    public async Task<DayCloseReportData> LoadAsync(
        string storeId,
        DateTime localDate,
        string? posCounterFilter,
        string? storeDisplayName = null,
        CancellationToken ct = default)
    {
        var businessDate = DaySessionService.FormatBusinessDate(localDate);
        DaySessionRecord? session = null;
        if (!string.IsNullOrWhiteSpace(posCounterFilter))
            session = await _daySessions.GetSessionAsync(storeId, businessDate, posCounterFilter, ct);

        var snapshot = await _daySessions.LoadDayCloseWithSessionAsync(storeId, localDate, posCounterFilter, ct);

        StoreDaySessionRollup? rollup = null;
        rollup = await _daySessions.GetStoreRollupAsync(storeId, businessDate, ct);

        var billSnapshot = await _billList.LoadAsync(storeId, new StoreBillListQuery
        {
            BusinessDate = localDate.Date,
            UseDateRange = false,
            PosCounterFilter = posCounterFilter,
            Limit = StoreBillListService.DefaultLimit,
        }, ct);

        var storeFilter = Builders<BsonDocument>.Filter.Eq("storeId", storeId);
        var returnDocs = await _db.GetCollection<BsonDocument>("store_sale_returns").Find(storeFilter).ToListAsync(ct);
        var adjustmentDocs = await _db.GetCollection<BsonDocument>("store_adjustments").Find(storeFilter).ToListAsync(ct);
        var expenseDocs = await _db.GetCollection<BsonDocument>("store_daily_expenses").Find(storeFilter).ToListAsync(ct);
        var movementDocs = await _db.GetCollection<BsonDocument>("store_cash_movements").Find(storeFilter).ToListAsync(ct);
        var cashoutDocs = await _db.GetCollection<BsonDocument>("store_credit_note_cashouts").Find(storeFilter).ToListAsync(ct);
        var sessionDocs = await _db.GetCollection<BsonDocument>("store_day_sessions").Find(
            Builders<BsonDocument>.Filter.And(
                storeFilter,
                Builders<BsonDocument>.Filter.Eq("businessDate", businessDate))).ToListAsync(ct);

        var returns = MapReturns(returnDocs, localDate, posCounterFilter);
        var adjustments = MapAdjustments(adjustmentDocs, localDate, posCounterFilter);
        var expenses = MapExpenses(expenseDocs, businessDate, posCounterFilter);
        var movements = MapCashMovements(movementDocs, businessDate, posCounterFilter);
        var cashouts = MapCreditNoteCashouts(cashoutDocs, localDate, posCounterFilter);
        var denominations = MapDenominations(sessionDocs, posCounterFilter);

        var counterScope = string.IsNullOrWhiteSpace(posCounterFilter)
            ? "All counters"
            : $"POS{posCounterFilter.Trim()}";

        return new DayCloseReportData
        {
            Metadata = new DayCloseReportMetadata
            {
                StoreId = storeId,
                StoreName = storeDisplayName ?? storeId,
                BusinessDate = businessDate,
                CounterScope = counterScope,
                SessionStatus = snapshot.SessionStatus ?? session?.Status ?? "—",
                ExportedAtLocal = DateTime.Now.ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture),
            },
            Snapshot = snapshot,
            Session = session,
            StoreRollup = rollup,
            Bills = billSnapshot.Rows,
            Returns = returns,
            Adjustments = adjustments,
            Expenses = expenses,
            CashMovements = movements,
            CreditNoteCashouts = cashouts,
            Denominations = denominations,
            StockExceptions = snapshot.StockExceptions,
        };
    }

    private static List<DayCloseReportReturnRow> MapReturns(
        IEnumerable<BsonDocument> docs,
        DateTime localDate,
        string? posCounterFilter)
    {
        var rows = new List<DayCloseReportReturnRow>();
        foreach (var doc in docs)
        {
            if (!DayBillingCloseDocumentReader.IsPostedReturn(doc))
                continue;
            if (!DayBillingCloseDocumentReader.MatchesLocalDay(doc, localDate))
                continue;
            if (!DayBillingCloseDocumentReader.MatchesPosCounterFilter(doc, posCounterFilter))
                continue;

            DayBillingCloseDocumentReader.TryGetUtcDate(doc, "createdAtUtc", out var sortUtc);
            var pos = DayBillingCloseDocumentReader.ReadString(doc, "posCounter") ?? "";
            var dev = DayBillingCloseDocumentReader.ReadString(doc, "deviceId") ?? "";

            rows.Add(new DayCloseReportReturnRow
            {
                ReturnNo = DayBillingCloseDocumentReader.ReadString(doc, "returnNo") ?? "",
                OriginalBillNo = DayBillingCloseDocumentReader.ReadString(doc, "originalBillNo") ?? "",
                CounterDisplay = CounterDisplayFormatter.Format(pos, dev),
                PostedAtLocal = sortUtc == default
                    ? "—"
                    : sortUtc.ToLocalTime().ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture),
                ReturnTotal = DayBillingCloseDocumentReader.ReadDecimal(doc, "returnTotal"),
                ReturnMode = DayBillingCloseDocumentReader.ReadString(doc, "returnMode") ?? "",
                CashRefunded = DayBillingCloseDocumentReader.ReadDecimal(doc, "cashRefunded"),
                CreditBalance = DayBillingCloseDocumentReader.ReadDecimal(doc, "creditBalance"),
                AmountCollected = DayBillingCloseDocumentReader.ReadDecimal(doc, "amountCollected"),
                PaymentSummary = DayBillingCloseDocumentReader.FormatReturnPaymentSummary(doc),
                CreditNoteNo = DayBillingCloseDocumentReader.ReadString(doc, "creditNoteNo") ?? "",
            });
        }

        return rows;
    }

    private static List<DayCloseReportAdjustmentRow> MapAdjustments(
        IEnumerable<BsonDocument> docs,
        DateTime localDate,
        string? posCounterFilter)
    {
        var rows = new List<DayCloseReportAdjustmentRow>();
        foreach (var doc in DayBillingCloseDocumentReader.FilterAdjustmentsForLocalDay(docs, localDate, posCounterFilter))
        {
            DayBillingCloseDocumentReader.TryGetUtcDate(doc, "createdAtUtc", out var sortUtc);
            var pos = DayBillingCloseDocumentReader.ReadString(doc, "posCounter") ?? "";
            var dev = DayBillingCloseDocumentReader.ReadString(doc, "deviceId") ?? "";

            rows.Add(new DayCloseReportAdjustmentRow
            {
                AdjustmentNo = DayBillingCloseDocumentReader.ReadString(doc, "adjustmentNo") ?? "",
                OriginalBillNo = DayBillingCloseDocumentReader.ReadString(doc, "originalBillNo") ?? "",
                CounterDisplay = CounterDisplayFormatter.Format(pos, dev),
                PostedAtLocal = sortUtc == default
                    ? "—"
                    : sortUtc.ToLocalTime().ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture),
                OriginalPayable = DayBillingCloseDocumentReader.ReadDecimal(doc, "originalPayable"),
                AdjustedPayable = DayBillingCloseDocumentReader.ReadDecimal(doc, "adjustedPayable"),
                DiffPayable = DayBillingCloseDocumentReader.ReadDecimal(doc, "diffPayable"),
                Reason = DayBillingCloseDocumentReader.ReadString(doc, "reason") ?? "",
            });
        }

        return rows;
    }

    private static List<DayCloseReportExpenseRow> MapExpenses(
        IEnumerable<BsonDocument> docs,
        string businessDate,
        string? posCounterFilter)
    {
        var rows = new List<DayCloseReportExpenseRow>();
        foreach (var doc in DayBillingCloseDocumentReader.FilterExpensesForBusinessDate(docs, businessDate, posCounterFilter))
        {
            var pos = DayBillingCloseDocumentReader.ReadString(doc, "posCounter") ?? "";
            var dev = DayBillingCloseDocumentReader.ReadString(doc, "deviceId") ?? "";
            rows.Add(new DayCloseReportExpenseRow
            {
                ExpenseNo = DayBillingCloseDocumentReader.ReadString(doc, "expenseNo") ?? "",
                CounterDisplay = CounterDisplayFormatter.Format(pos, dev),
                BusinessDate = DayBillingCloseDocumentReader.ReadString(doc, "businessDate") ?? businessDate,
                Description = DayBillingCloseDocumentReader.ReadString(doc, "description") ?? "",
                Amount = DayBillingCloseDocumentReader.ReadDecimal(doc, "amount"),
            });
        }

        return rows;
    }

    private static List<DayCloseReportCashMovementRow> MapCashMovements(
        IEnumerable<BsonDocument> docs,
        string businessDate,
        string? posCounterFilter)
    {
        var rows = new List<DayCloseReportCashMovementRow>();
        foreach (var doc in DayBillingCloseDocumentReader.FilterCashMovementsForBusinessDate(docs, businessDate, posCounterFilter))
        {
            var pos = DayBillingCloseDocumentReader.ReadString(doc, "posCounter") ?? "";
            var dev = DayBillingCloseDocumentReader.ReadString(doc, "deviceId") ?? "";
            var type = DayBillingCloseDocumentReader.ReadString(doc, "movementType") ?? "";
            var typeDisplay = string.Equals(type, CashMovementType.DepositToBank, StringComparison.OrdinalIgnoreCase)
                ? "Deposit to bank"
                : string.Equals(type, CashMovementType.CashWithdrawal, StringComparison.OrdinalIgnoreCase)
                    ? "Cash withdrawal"
                    : type;

            rows.Add(new DayCloseReportCashMovementRow
            {
                MovementNo = DayBillingCloseDocumentReader.ReadString(doc, "movementNo") ?? "",
                MovementType = typeDisplay,
                CounterDisplay = CounterDisplayFormatter.Format(pos, dev),
                Amount = DayBillingCloseDocumentReader.ReadDecimal(doc, "amount"),
                Note = DayBillingCloseDocumentReader.ReadString(doc, "description") ?? "",
                PostedAtLocal = DayBillingCloseDocumentReader.FormatUtcLocal(
                    DayBillingCloseDocumentReader.ReadString(doc, "createdAtUtc")),
            });
        }

        return rows;
    }

    private static List<DayCloseReportCreditNoteCashoutRow> MapCreditNoteCashouts(
        IEnumerable<BsonDocument> docs,
        DateTime localDate,
        string? posCounterFilter)
    {
        var rows = new List<DayCloseReportCreditNoteCashoutRow>();
        foreach (var doc in DayBillingCloseDocumentReader.FilterCreditNoteCashoutsForLocalDay(docs, localDate, posCounterFilter))
        {
            var pos = DayBillingCloseDocumentReader.ReadString(doc, "posCounter") ?? "";
            var dev = DayBillingCloseDocumentReader.ReadString(doc, "deviceId") ?? "";
            rows.Add(new DayCloseReportCreditNoteCashoutRow
            {
                CashoutNo = DayBillingCloseDocumentReader.ReadString(doc, "cashoutNo") ?? "",
                CreditNoteNo = DayBillingCloseDocumentReader.ReadString(doc, "creditNoteNo") ?? "",
                Amount = DayBillingCloseDocumentReader.ReadDecimal(doc, "cashRefunded"),
                CounterDisplay = CounterDisplayFormatter.Format(pos, dev),
                PostedAtLocal = DayBillingCloseDocumentReader.FormatUtcLocal(
                    DayBillingCloseDocumentReader.ReadString(doc, "createdAtUtc")),
            });
        }

        return rows;
    }

    private static List<DayCloseReportDenominationRow> MapDenominations(
        IEnumerable<BsonDocument> sessionDocs,
        string? posCounterFilter)
    {
        var rows = new List<DayCloseReportDenominationRow>();
        foreach (var doc in sessionDocs)
        {
            var record = DaySessionDocumentMapper.ToRecord(doc);
            if (record == null)
                continue;
            if (!string.Equals(record.Status, DaySessionStatus.Closed, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(posCounterFilter)
                && !string.Equals(record.PosCounter, posCounterFilter.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            var counterDisplay = CounterDisplayFormatter.Format(record.PosCounter, record.DeviceId);
            foreach (var line in record.CashDenominations.Where(d => d.UnitCount > 0))
            {
                rows.Add(new DayCloseReportDenominationRow
                {
                    CounterDisplay = counterDisplay,
                    Denomination = line.Denomination,
                    UnitCount = line.UnitCount,
                    Subtotal = line.Amount,
                });
            }
        }

        return rows;
    }
}
