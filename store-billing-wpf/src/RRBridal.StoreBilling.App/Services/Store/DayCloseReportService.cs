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
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Sync;
using RRBridal.StoreBilling.App.Services.Expenses;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class DayCloseReportService
{
    private readonly IMongoDatabase _db;
    private readonly DaySessionService _daySessions;
    private CentralOnlineModeService? _centralMode;
    private CentralDashboardClient? _dashboardApi;
    private CentralStorePosClient? _storePos;

    public DayCloseReportService(
        IMongoDatabase localDb,
        DaySessionService daySessions)
    {
        _db = localDb;
        _daySessions = daySessions;
    }

    public void ConfigureOnline(
        CentralOnlineModeService centralMode,
        CentralDashboardClient dashboardApi,
        CentralStorePosClient? storePos = null)
    {
        _centralMode = centralMode;
        _dashboardApi = dashboardApi;
        _storePos = storePos;
    }

    public Task<DayCloseReportData> LoadAsync(
        string storeId,
        DateTime localDate,
        string? posCounterFilter,
        string? storeDisplayName = null,
        CancellationToken ct = default)
    {
        if (_centralMode?.IsOnlineMode == true)
            return LoadOnlineAsync(storeId, localDate, posCounterFilter, storeDisplayName, ct);

        return LoadOfflineAsync(storeId, localDate, posCounterFilter, storeDisplayName, ct);
    }

    private async Task<DayCloseReportData> LoadOfflineAsync(
        string storeId,
        DateTime localDate,
        string? posCounterFilter,
        string? storeDisplayName,
        CancellationToken ct)
    {
        var businessDate = DaySessionService.FormatBusinessDate(localDate);
        DaySessionRecord? session = null;
        if (!string.IsNullOrWhiteSpace(posCounterFilter))
            session = await _daySessions.GetSessionAsync(storeId, businessDate, posCounterFilter, ct);

        var snapshot = await _daySessions.LoadDayCloseWithSessionAsync(storeId, localDate, posCounterFilter, ct);

        StoreDaySessionRollup? rollup = null;
        rollup = await _daySessions.GetStoreRollupAsync(storeId, businessDate, ct);

        var storeFilter = Builders<BsonDocument>.Filter.Eq("storeId", storeId);
        var billDocs = await _db.GetCollection<BsonDocument>("store_bills").Find(storeFilter).ToListAsync(ct);
        var returnDocs = await _db.GetCollection<BsonDocument>("store_sale_returns").Find(storeFilter).ToListAsync(ct);
        var adjustmentDocs = await _db.GetCollection<BsonDocument>("store_adjustments").Find(storeFilter).ToListAsync(ct);
        var expenseDocs = await _db.GetCollection<BsonDocument>("store_daily_expenses").Find(storeFilter).ToListAsync(ct);
        var movementDocs = await _db.GetCollection<BsonDocument>("store_cash_movements").Find(storeFilter).ToListAsync(ct);
        var cashoutDocs = await _db.GetCollection<BsonDocument>("store_credit_note_cashouts").Find(storeFilter).ToListAsync(ct);
        var sessionDocs = await _db.GetCollection<BsonDocument>("store_day_sessions").Find(
            Builders<BsonDocument>.Filter.And(
                storeFilter,
                Builders<BsonDocument>.Filter.Eq("businessDate", businessDate))).ToListAsync(ct);

        var outboxDocs = await _db.GetCollection<BsonDocument>("outbox_events")
            .Find(Builders<BsonDocument>.Filter.And(
                storeFilter,
                Builders<BsonDocument>.Filter.Eq("type", "InvoiceCreated")))
            .ToListAsync(ct);
        var outboxByBillNo = DayBillingCloseDocumentReader.BuildOutboxSyncByBillNo(outboxDocs);

        var returnNoByBillNo = returnDocs
            .Where(DayBillingCloseDocumentReader.IsPostedReturn)
            .GroupBy(d => DayBillingCloseDocumentReader.ReadString(d, "originalBillNo") ?? "")
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .ToDictionary(
                g => g.Key,
                g => DayBillingCloseDocumentReader.ReadString(g.First(), "returnNo") ?? "",
                StringComparer.OrdinalIgnoreCase);

        var bills = BuildDayCloseBillRows(
            billDocs,
            localDate,
            posCounterFilter,
            outboxByBillNo,
            returnNoByBillNo);

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
            Bills = bills,
            Returns = returns,
            Adjustments = adjustments,
            Expenses = expenses,
            CashMovements = movements,
            CreditNoteCashouts = cashouts,
            Denominations = denominations,
            StockExceptions = snapshot.StockExceptions,
        };
    }

    private async Task<DayCloseReportData> LoadOnlineAsync(
        string storeId,
        DateTime localDate,
        string? posCounterFilter,
        string? storeDisplayName,
        CancellationToken ct)
    {
        if (_storePos == null)
            throw new InvalidOperationException("Central store-pos client is not configured for online mode.");

        var businessDate = DaySessionService.FormatBusinessDate(localDate);
        DaySessionRecord? session = null;
        if (!string.IsNullOrWhiteSpace(posCounterFilter))
            session = await _daySessions.GetSessionAsync(storeId, businessDate, posCounterFilter, ct);

        var snapshot = await _daySessions.LoadDayCloseWithSessionAsync(storeId, localDate, posCounterFilter, ct);
        var rollup = await _daySessions.GetStoreRollupAsync(storeId, businessDate, ct);

        using var billsJson = await _storePos.ListBillsAsync(null, 200, ct);
        var billDocs = ExtractDocs(billsJson, BillDocumentService.MapCentralBillToDoc);

        using var returnsJson = await _storePos.ListSaleReturnsAsync(null, 200, ct);
        var returnDocs = ExtractDocs(returnsJson, MapCentralReturnToDoc);

        var returnNoByBillNo = returnDocs
            .Where(DayBillingCloseDocumentReader.IsPostedReturn)
            .GroupBy(d => DayBillingCloseDocumentReader.ReadString(d, "originalBillNo") ?? "")
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .ToDictionary(
                g => g.Key,
                g => DayBillingCloseDocumentReader.ReadString(g.First(), "returnNo") ?? "",
                StringComparer.OrdinalIgnoreCase);

        var billRows = BuildDayCloseBillRows(
            billDocs,
            localDate,
            posCounterFilter,
            outboxByBillNo: new Dictionary<string, string>(),
            returnNoByBillNo);

        using var expensesJson = await _storePos.ListDailyExpensesAsync(businessDate, 200, ct);
        var expenseDocs = ExtractDocs(expensesJson, MapCentralExpenseToDoc);

        using var movementsJson = await _storePos.ListCashMovementsAsync(businessDate, 200, ct);
        var movementDocs = ExtractDocs(movementsJson, MapCentralMovementToDoc);

        var returns = MapReturns(returnDocs, localDate, posCounterFilter);
        var expenses = MapExpenses(expenseDocs, businessDate, posCounterFilter);
        var movements = MapCashMovements(movementDocs, businessDate, posCounterFilter);

        var adjustments = Array.Empty<DayCloseReportAdjustmentRow>() as IReadOnlyList<DayCloseReportAdjustmentRow>;
        var cashouts = Array.Empty<DayCloseReportCreditNoteCashoutRow>() as IReadOnlyList<DayCloseReportCreditNoteCashoutRow>;
        var denominations = Array.Empty<DayCloseReportDenominationRow>() as IReadOnlyList<DayCloseReportDenominationRow>;

        if (_dashboardApi != null)
        {
            using var reportJson = await _dashboardApi.GetStoreDayCloseReportAsync(businessDate, posCounterFilter, ct);
            adjustments = MapOnlineReportAdjustments(reportJson.RootElement);
            cashouts = MapOnlineReportCashouts(reportJson.RootElement);
            denominations = MapOnlineReportDenominations(reportJson.RootElement);
        }

        // Fallback: session denominations when report omitted them
        if (denominations.Count == 0 && session != null
            && string.Equals(session.Status, DaySessionStatus.Closed, StringComparison.OrdinalIgnoreCase))
        {
            denominations = session.CashDenominations
                .Where(d => d.UnitCount > 0)
                .Select(d => new DayCloseReportDenominationRow
                {
                    CounterDisplay = CounterDisplayFormatter.Format(session.PosCounter, session.DeviceId),
                    Denomination = d.Denomination,
                    UnitCount = d.UnitCount,
                    Subtotal = d.Amount,
                })
                .ToList();
        }

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
            Bills = billRows,
            Returns = returns,
            Adjustments = adjustments,
            Expenses = expenses,
            CashMovements = movements,
            CreditNoteCashouts = cashouts,
            Denominations = denominations,
            StockExceptions = snapshot.StockExceptions,
        };
    }

    private static IReadOnlyList<DayCloseReportAdjustmentRow> MapOnlineReportAdjustments(JsonElement root)
    {
        if (!root.TryGetProperty("adjustments", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return Array.Empty<DayCloseReportAdjustmentRow>();

        var rows = new List<DayCloseReportAdjustmentRow>();
        foreach (var el in arr.EnumerateArray())
        {
            rows.Add(new DayCloseReportAdjustmentRow
            {
                AdjustmentNo = ReadReportString(el, "adjustmentNo"),
                OriginalBillNo = ReadReportString(el, "originalBill"),
                CounterDisplay = ReadReportString(el, "counter"),
                PostedAtLocal = ReadReportString(el, "postedAtLocal"),
                OriginalPayable = ParseReportMoney(el, "originalPayable"),
                AdjustedPayable = ParseReportMoney(el, "adjustedPayable"),
                DiffPayable = ParseReportMoney(el, "diffPayable"),
                Reason = ReadReportString(el, "reason"),
            });
        }

        return rows;
    }

    private static IReadOnlyList<DayCloseReportCreditNoteCashoutRow> MapOnlineReportCashouts(JsonElement root)
    {
        if (!root.TryGetProperty("creditNoteCashouts", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return Array.Empty<DayCloseReportCreditNoteCashoutRow>();

        var rows = new List<DayCloseReportCreditNoteCashoutRow>();
        foreach (var el in arr.EnumerateArray())
        {
            rows.Add(new DayCloseReportCreditNoteCashoutRow
            {
                CashoutNo = ReadReportString(el, "cashoutNo"),
                CreditNoteNo = ReadReportString(el, "creditNoteNo"),
                Amount = ParseReportMoney(el, "amount"),
                CounterDisplay = ReadReportString(el, "counter"),
                PostedAtLocal = ReadReportString(el, "postedAtLocal"),
            });
        }

        return rows;
    }

    private static IReadOnlyList<DayCloseReportDenominationRow> MapOnlineReportDenominations(JsonElement root)
    {
        if (!root.TryGetProperty("denominations", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return Array.Empty<DayCloseReportDenominationRow>();

        var rows = new List<DayCloseReportDenominationRow>();
        foreach (var el in arr.EnumerateArray())
        {
            _ = int.TryParse(ReadReportString(el, "denomination"), NumberStyles.Any, CultureInfo.InvariantCulture, out var denom);
            _ = int.TryParse(ReadReportString(el, "count"), NumberStyles.Any, CultureInfo.InvariantCulture, out var count);
            rows.Add(new DayCloseReportDenominationRow
            {
                CounterDisplay = ReadReportString(el, "counter"),
                Denomination = denom,
                UnitCount = count,
                Subtotal = ParseReportMoney(el, "subtotal"),
            });
        }

        return rows;
    }

    private static List<StoreBillListRow> BuildDayCloseBillRows(
        IEnumerable<BsonDocument> billDocs,
        DateTime localDate,
        string? posCounterFilter,
        IReadOnlyDictionary<string, string> outboxByBillNo,
        IReadOnlyDictionary<string, string> returnNoByBillNo)
    {
        var rows = new List<StoreBillListRow>();
        var posted = billDocs
            .Where(DayBillingCloseDocumentReader.IsPostedBill)
            .Where(d => DayBillingCloseDocumentReader.MatchesPosCounterFilter(d, posCounterFilter))
            .ToList();

        foreach (var doc in posted.Where(d => DayBillingCloseDocumentReader.MatchesLocalDay(d, localDate)))
        {
            var payments = DayBillingCloseDocumentReader.SumBillPaymentsForLocalDay(doc, localDate);
            rows.Add(MapDayCloseBillRow(doc, payments, localDate, outboxByBillNo, returnNoByBillNo, creditCollected: false));
        }

        foreach (var doc in posted
            .Where(DayBillingCloseDocumentReader.HasCreditBilling)
            .Where(d => !DayBillingCloseDocumentReader.MatchesLocalDay(d, localDate)))
        {
            var payments = DayBillingCloseDocumentReader.SumBillPaymentsForLocalDay(doc, localDate);
            if (payments.Cash <= 0 && payments.Card <= 0 && payments.Upi <= 0 && payments.CreditNote <= 0)
                continue;
            rows.Add(MapDayCloseBillRow(doc, payments, localDate, outboxByBillNo, returnNoByBillNo, creditCollected: true));
        }

        rows.Sort((a, b) => b.SortUtc.CompareTo(a.SortUtc));
        return rows;
    }

    private static StoreBillListRow MapDayCloseBillRow(
        BsonDocument doc,
        PaymentDayTotals payments,
        DateTime localDate,
        IReadOnlyDictionary<string, string> outboxByBillNo,
        IReadOnlyDictionary<string, string> returnNoByBillNo,
        bool creditCollected)
    {
        var billNo = DayBillingCloseDocumentReader.ReadString(doc, "billNo") ?? "";
        DateTime sortUtc;
        string postedLocal;
        if (creditCollected)
        {
            sortUtc = DateTime.MinValue;
            if (doc.TryGetValue("creditBilling", out var cbVal) && cbVal.IsBsonDocument
                && cbVal.AsBsonDocument.TryGetValue("payments", out var payVal) && payVal.IsBsonArray)
            {
                foreach (BsonDocument p in payVal.AsBsonArray.OfType<BsonDocument>())
                {
                    if (!DayBillingCloseDocumentReader.TryGetUtcDate(p, "receivedAtUtc", out var received)
                        && !DayBillingCloseDocumentReader.TryGetUtcDate(p, "receivedAt", out received)
                        && !DayBillingCloseDocumentReader.TryGetUtcDate(p, "createdAtUtc", out received))
                        continue;
                    if (received.ToLocalTime().Date != localDate.Date)
                        continue;
                    if (received > sortUtc)
                        sortUtc = received;
                }
            }

            postedLocal = sortUtc == DateTime.MinValue
                ? "—"
                : sortUtc.ToLocalTime().ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture);
        }
        else
        {
            sortUtc = DayBillingCloseDocumentReader.TryGetUtcDate(doc, "createdAtUtc", out var utc)
                ? utc
                : DateTime.MinValue;
            postedLocal = sortUtc == DateTime.MinValue
                ? "—"
                : sortUtc.ToLocalTime().ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        var pos = DayBillingCloseDocumentReader.ReadString(doc, "posCounter") ?? "";
        var dev = DayBillingCloseDocumentReader.ReadString(doc, "deviceId") ?? "";
        var hasReturn = returnNoByBillNo.TryGetValue(billNo, out var returnNo);
        var syncStatus = outboxByBillNo.Count == 0
            ? "Synced"
            : DayBillingCloseDocumentReader.ResolveSyncStatus(doc, outboxByBillNo);

        var collected = payments.Cash + payments.Card + payments.Upi + payments.CreditNote;
        return new StoreBillListRow
        {
            BillNo = billNo,
            BillDate = DayBillingCloseDocumentReader.ReadString(doc, "billDate") ?? "",
            CustomerName = DayBillingCloseDocumentReader.ReadString(doc, "customerName") ?? "",
            CustomerPhone = DayBillingCloseDocumentReader.ReadString(doc, "customerPhone") ?? "",
            SalesmanCode = DayBillingCloseDocumentReader.ReadString(doc, "salesmanCode") ?? "",
            SalesmanName = DayBillingCloseDocumentReader.ReadString(doc, "salesman") ?? "",
            SalesmanId = DayBillingCloseDocumentReader.ReadString(doc, "salesmanId") ?? "",
            CounterDisplay = CounterDisplayFormatter.Format(pos, dev),
            PostedAtLocal = postedLocal,
            TotalQty = creditCollected ? 0m : DayBillingCloseDocumentReader.SumBillLineQty(doc),
            Payable = creditCollected ? collected : DayBillingCloseDocumentReader.ReadDecimal(doc, "payable"),
            CashAmount = payments.Cash,
            CardAmount = payments.Card,
            UpiAmount = payments.Upi,
            CreditNoteAmount = payments.CreditNote,
            CreditNoteRefs = DayBillingCloseDocumentReader.FormatBillCreditNoteReferences(doc),
            SyncStatus = creditCollected
                ? $"{syncStatus} · credit collected"
                : syncStatus,
            HasReturn = hasReturn,
            ReturnNo = returnNo ?? "",
            HasAdjustment = false,
            AdjustmentNo = "",
            SortUtc = sortUtc,
        };
    }

    private static string ReadReportString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString() ?? ""
            : el.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.Number
                ? n.ToString()
                : "";

    private static decimal ParseReportMoney(JsonElement el, string name)
    {
        var s = ReadReportString(el, name).Replace("₹", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal).Trim();
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }

    private static List<BsonDocument> ExtractDocs(JsonDocument json, Func<JsonElement, BsonDocument> mapper)
    {
        var docs = new List<BsonDocument>();
        if (json.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in json.RootElement.EnumerateArray())
                docs.Add(mapper(el));
        }

        return docs;
    }

    private static BsonDocument MapCentralReturnToDoc(JsonElement el)
    {
        BsonDocument doc;
        if (el.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
            doc = BsonDocument.Parse(payload.GetRawText());
        else
            doc = BsonDocument.Parse(el.GetRawText());

        if (el.TryGetProperty("returnNo", out var returnNoEl) && returnNoEl.ValueKind == JsonValueKind.String)
            doc["returnNo"] = returnNoEl.GetString() ?? "";
        if (el.TryGetProperty("storeId", out var storeIdEl) && storeIdEl.ValueKind == JsonValueKind.String)
            doc["storeId"] = storeIdEl.GetString() ?? "";
        if (!doc.Contains("status"))
            doc["status"] = "posted";

        return doc;
    }

    private static BsonDocument MapCentralExpenseToDoc(JsonElement el)
    {
        BsonDocument doc;
        if (el.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
            doc = BsonDocument.Parse(payload.GetRawText());
        else
            doc = BsonDocument.Parse(el.GetRawText());

        if (el.TryGetProperty("expenseNo", out var noEl) && noEl.ValueKind == JsonValueKind.String)
            doc["expenseNo"] = noEl.GetString() ?? "";
        if (el.TryGetProperty("storeId", out var storeIdEl) && storeIdEl.ValueKind == JsonValueKind.String)
            doc["storeId"] = storeIdEl.GetString() ?? "";
        if (!doc.Contains("status"))
            doc["status"] = "posted";

        return doc;
    }

    private static BsonDocument MapCentralMovementToDoc(JsonElement el)
    {
        BsonDocument doc;
        if (el.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
            doc = BsonDocument.Parse(payload.GetRawText());
        else
            doc = BsonDocument.Parse(el.GetRawText());

        if (el.TryGetProperty("movementNo", out var noEl) && noEl.ValueKind == JsonValueKind.String)
            doc["movementNo"] = noEl.GetString() ?? "";
        if (el.TryGetProperty("storeId", out var storeIdEl) && storeIdEl.ValueKind == JsonValueKind.String)
            doc["storeId"] = storeIdEl.GetString() ?? "";
        if (!doc.Contains("status"))
            doc["status"] = "posted";

        return doc;
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
                SupplierName = DayBillingCloseDocumentReader.ReadString(doc, "supplierName") ?? "",
                SupplierGstin = DayBillingCloseDocumentReader.ReadString(doc, "supplierGstin") ?? "",
                SupplierInvoiceNo = DayBillingCloseDocumentReader.ReadString(doc, "supplierInvoiceNo") ?? "",
                SupplierInvoiceDate = DayBillingCloseDocumentReader.ReadString(doc, "supplierInvoiceDate") ?? "",
                Category = DayBillingCloseDocumentReader.ReadString(doc, "category") ?? "",
                GstMode = DayBillingCloseDocumentReader.ReadString(doc, "gstMode") ?? ExpenseGstMode.None,
                GstRate = DayBillingCloseDocumentReader.ReadDecimal(doc, "gstRate"),
                TaxableAmount = ReadExpenseTaxable(doc),
                CgstAmount = DayBillingCloseDocumentReader.ReadDecimal(doc, "cgstAmount"),
                SgstAmount = DayBillingCloseDocumentReader.ReadDecimal(doc, "sgstAmount"),
                IgstAmount = DayBillingCloseDocumentReader.ReadDecimal(doc, "igstAmount"),
                PaymentSummary = FormatExpensePayments(doc),
                PostedAtLocal = DayBillingCloseDocumentReader.FormatUtcLocal(
                    DayBillingCloseDocumentReader.ReadString(doc, "createdAtUtc")),
                Amount = DayBillingCloseDocumentReader.ReadDecimal(doc, "amount"),
            });
        }

        return rows;
    }

    private static decimal ReadExpenseTaxable(BsonDocument doc)
    {
        var taxable = DayBillingCloseDocumentReader.ReadDecimal(doc, "taxableAmount");
        return taxable > 0 ? taxable : DayBillingCloseDocumentReader.ReadDecimal(doc, "amount");
    }

    private static string FormatExpensePayments(BsonDocument doc)
    {
        if (!doc.TryGetValue("payments", out var payments) || !payments.IsBsonArray || payments.AsBsonArray.Count == 0)
            return $"Cash {DayBillingCloseDocumentReader.ReadDecimal(doc, "amount"):0.00}";
        return string.Join(", ", payments.AsBsonArray.OfType<BsonDocument>()
            .Where(p => DayBillingCloseDocumentReader.ReadDecimal(p, "amount") > 0)
            .Select(p =>
                $"{DayBillingCloseDocumentReader.ReadString(p, "mode") ?? DayBillingCloseDocumentReader.ReadString(p, "provider") ?? "Other"} " +
                $"{DayBillingCloseDocumentReader.ReadDecimal(p, "amount"):0.00}"));
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
