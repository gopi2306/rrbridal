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
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Products;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class DayBillingCloseService
{
    private readonly IMongoDatabase _db;
    private readonly ProductCatalogService _productCatalog;
    private readonly StoreAuditLogService? _auditLog;
    private CentralOnlineModeService? _centralMode;
    private CentralDashboardClient? _dashboardApi;
    private CentralStorePosClient? _storePos;

    public DayBillingCloseService(
        IMongoDatabase localDb,
        ProductCatalogService productCatalog,
        StoreAuditLogService? auditLog = null)
    {
        _db = localDb;
        _productCatalog = productCatalog;
        _auditLog = auditLog;
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

    public Task<DayBillingCloseSnapshot> LoadDayCloseAsync(
        string storeId,
        DateTime localDate,
        string? posCounterFilter = null,
        DaySessionRecord? session = null,
        CancellationToken ct = default)
    {
        if (_centralMode?.IsOnlineMode == true)
            return LoadDayCloseOnlineAsync(storeId, localDate, posCounterFilter, session, ct);

        return LoadDayCloseOfflineAsync(storeId, localDate, posCounterFilter, session, ct);
    }

    private async Task<DayBillingCloseSnapshot> LoadDayCloseOfflineAsync(
        string storeId,
        DateTime localDate,
        string? posCounterFilter,
        DaySessionRecord? session,
        CancellationToken ct)
    {
        var billsColl = _db.GetCollection<BsonDocument>("store_bills");
        var returnsColl = _db.GetCollection<BsonDocument>("store_sale_returns");
        var cashoutsColl = _db.GetCollection<BsonDocument>("store_credit_note_cashouts");
        var expensesColl = _db.GetCollection<BsonDocument>("store_daily_expenses");
        var movementsColl = _db.GetCollection<BsonDocument>("store_cash_movements");
        var outboxColl = _db.GetCollection<BsonDocument>("outbox_events");

        var storeFilter = Builders<BsonDocument>.Filter.Eq("storeId", storeId);
        var billDocs = await billsColl.Find(storeFilter).ToListAsync(ct);
        var returnDocs = await returnsColl.Find(storeFilter).ToListAsync(ct);
        var cashoutDocs = await cashoutsColl.Find(storeFilter).ToListAsync(ct);

        var outboxDocs = await outboxColl
            .Find(Builders<BsonDocument>.Filter.And(
                storeFilter,
                Builders<BsonDocument>.Filter.Eq("type", "InvoiceCreated")))
            .ToListAsync(ct);
        var outboxByBillNo = DayBillingCloseDocumentReader.BuildOutboxSyncByBillNo(outboxDocs);

        var dayBills = billDocs
            .Where(DayBillingCloseDocumentReader.IsPostedBill)
            .Where(d => DayBillingCloseDocumentReader.MatchesLocalDay(d, localDate))
            .Where(d => DayBillingCloseDocumentReader.MatchesPosCounterFilter(d, posCounterFilter))
            .ToList();

        var invoices = new List<DayCloseInvoiceRow>();
        var stockExceptions = new List<DayCloseStockExceptionRow>();
        decimal totalQty = 0m, totalAmount = 0m;
        decimal cash = 0m, card = 0m, upi = 0m, creditNote = 0m;

        foreach (var doc in dayBills)
        {
            var billNo = DayBillingCloseDocumentReader.ReadString(doc, "billNo") ?? "";
            var payable = DayBillingCloseDocumentReader.ReadDecimal(doc, "payable");
            var qty = DayBillingCloseDocumentReader.SumBillLineQty(doc);
            var payments = DayBillingCloseDocumentReader.SumBillPayments(doc);

            totalQty += qty;
            totalAmount += payable;
            cash += payments.Cash;
            card += payments.Card;
            upi += payments.Upi;
            creditNote += payments.CreditNote;

            DayBillingCloseDocumentReader.TryGetUtcDate(doc, "createdAtUtc", out var sortUtc);
            var postedLocal = sortUtc == default
                ? "—"
                : sortUtc.ToLocalTime().ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture);

            var pos = DayBillingCloseDocumentReader.ReadString(doc, "posCounter") ?? "";
            var dev = DayBillingCloseDocumentReader.ReadString(doc, "deviceId") ?? "";
            var paymentMode = DayBillingCloseDocumentReader.ReadString(doc, "paymentMode") ?? "";

            invoices.Add(new DayCloseInvoiceRow
            {
                BillNo = billNo,
                CounterDisplay = CounterDisplayFormatter.Format(pos, dev),
                PostedAtLocal = postedLocal,
                TotalQty = qty,
                Payable = payable,
                PaymentMode = paymentMode,
                SyncStatus = DayBillingCloseDocumentReader.ResolveSyncStatus(doc, outboxByBillNo),
                SortUtc = sortUtc,
            });

            AppendStockExceptionRows(doc, billNo, pos, dev, postedLocal, stockExceptions);
        }

        var priorCodReceived = DayBillingCloseDocumentReader.AggregatePriorOnlineCodReceivedOnLocalDay(
            billDocs,
            localDate,
            posCounterFilter,
            outboxByBillNo);
        cash += priorCodReceived.Payments.Cash;
        card += priorCodReceived.Payments.Card;
        upi += priorCodReceived.Payments.Upi;
        creditNote += priorCodReceived.Payments.CreditNote;
        totalAmount += priorCodReceived.TotalAmount;
        invoices.AddRange(priorCodReceived.InvoiceRows);

        invoices.Sort((a, b) => b.SortUtc.CompareTo(a.SortUtc));

        var dayReturns = returnDocs
            .Where(DayBillingCloseDocumentReader.IsPostedReturn)
            .Where(d => DayBillingCloseDocumentReader.MatchesLocalDay(d, localDate))
            .Where(d => DayBillingCloseDocumentReader.MatchesPosCounterFilter(d, posCounterFilter))
            .ToList();

        var returnTotals = DayBillingCloseDocumentReader.AggregateReturnDayTotals(dayReturns);
        var exchangePayments = returnTotals.ExchangePayments;

        var dayCashouts = cashoutDocs
            .Where(DayBillingCloseDocumentReader.IsPostedCashout)
            .Where(d => DayBillingCloseDocumentReader.MatchesLocalDay(d, localDate))
            .Where(d => DayBillingCloseDocumentReader.MatchesPosCounterFilter(d, posCounterFilter))
            .ToList();
        var creditNoteCashoutTotal = DayBillingCloseDocumentReader.AggregateCreditNoteCashoutDayTotals(dayCashouts);
        var returnCashRefundTotal = returnTotals.CashRefundTotal;
        var cashRefundTotal = returnCashRefundTotal + creditNoteCashoutTotal;

        var businessDate = localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var expenseDocs = await expensesColl.Find(storeFilter).ToListAsync(ct);
        var dailyExpensesTotal = DayBillingCloseDocumentReader.SumDailyExpensesForBusinessDate(
            expenseDocs,
            businessDate,
            posCounterFilter);

        var movementDocs = await movementsColl.Find(storeFilter).ToListAsync(ct);
        var (depositsTotal, withdrawalsTotal) = DayBillingCloseDocumentReader.SumCashMovementsForBusinessDate(
            movementDocs,
            businessDate,
            posCounterFilter);

        var netCash = cash - cashRefundTotal + exchangePayments.Cash - dailyExpensesTotal;
        var netCard = card + exchangePayments.Card;
        var netUpi = upi + exchangePayments.Upi;
        var actualHandIn = netCash + netCard + netUpi;

        var openingCash = session?.OpeningCash ?? 0m;
        var expectedCash = DaySessionCashMath.ComputeExpectedCash(
            openingCash,
            netCash,
            depositsTotal,
            withdrawalsTotal);
        var actualCashCounted = session?.ActualCashCounted ?? 0m;
        var cashDifference = session != null && session.Status == DaySessionStatus.Closed
            ? session.CashDifference
            : actualCashCounted - expectedCash;

        var returnRows = new List<DayCloseReturnRow>();
        foreach (var doc in dayReturns)
        {
            DayBillingCloseDocumentReader.TryGetUtcDate(doc, "createdAtUtc", out var sortUtc);
            var postedLocal = sortUtc == default
                ? "—"
                : sortUtc.ToLocalTime().ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture);

            var pos = DayBillingCloseDocumentReader.ReadString(doc, "posCounter") ?? "";
            var dev = DayBillingCloseDocumentReader.ReadString(doc, "deviceId") ?? "";
            var returnMode = DayBillingCloseDocumentReader.ReadString(doc, "returnMode") ?? "";

            returnRows.Add(new DayCloseReturnRow
            {
                ReturnNo = DayBillingCloseDocumentReader.ReadString(doc, "returnNo") ?? "",
                OriginalBillNo = DayBillingCloseDocumentReader.ReadString(doc, "originalBillNo") ?? "",
                CounterDisplay = CounterDisplayFormatter.Format(pos, dev),
                PostedAtLocal = postedLocal,
                ReturnTotal = DayBillingCloseDocumentReader.ReadDecimal(doc, "returnTotal"),
                ReturnMode = returnMode,
                CreditBalance = DayBillingCloseDocumentReader.ReadDecimal(doc, "creditBalance"),
                CashRefunded = DayBillingCloseDocumentReader.ReadDecimal(doc, "cashRefunded"),
                AmountCollected = DayBillingCloseDocumentReader.ReadDecimal(doc, "amountCollected"),
                PaymentSummary = DayBillingCloseDocumentReader.FormatReturnPaymentSummary(doc),
                SortUtc = sortUtc,
            });
        }

        returnRows.Sort((a, b) => b.SortUtc.CompareTo(a.SortUtc));

        return new DayBillingCloseSnapshot
        {
            LocalDate = localDate.Date,
            BillCount = dayBills.Count,
            TotalQty = totalQty,
            TotalAmount = totalAmount,
            CashTotal = cash,
            CardTotal = card,
            UpiTotal = upi,
            CreditNoteTotal = creditNote,
            ReturnCount = returnTotals.ReturnCount,
            ReturnTotalAmount = returnTotals.ReturnTotalAmount,
            ReturnCashRefundTotal = returnCashRefundTotal,
            CreditNoteCashoutTotal = creditNoteCashoutTotal,
            CashRefundTotal = cashRefundTotal,
            CreditNoteIssuedTotal = returnTotals.CreditNoteIssuedTotal,
            NetCashInHand = netCash,
            NetCardInHand = netCard,
            NetUpiInHand = netUpi,
            ActualHandInTotal = actualHandIn,
            DailyExpensesTotal = dailyExpensesTotal,
            DepositsTotal = depositsTotal,
            WithdrawalsTotal = withdrawalsTotal,
            OpeningCash = openingCash,
            ExpectedCash = expectedCash,
            ActualCashCounted = actualCashCounted,
            CashDifference = cashDifference,
            SessionStatus = session?.Status,
            Invoices = invoices,
            Returns = returnRows,
            StockExceptions = stockExceptions,
        };
    }

    private async Task<DayBillingCloseSnapshot> LoadDayCloseOnlineAsync(
        string storeId,
        DateTime localDate,
        string? posCounterFilter,
        DaySessionRecord? session,
        CancellationToken ct)
    {
        if (_dashboardApi == null || _storePos == null)
            throw new InvalidOperationException("Central APIs are not configured for online day close.");

        var businessDate = localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        using var dayCloseJson = await _dashboardApi.GetStoreDayCloseAsync(businessDate, posCounterFilter, ct);
        var counterFigures = string.IsNullOrWhiteSpace(posCounterFilter)
            ? null
            : StoreDayCloseDashboardReader.FindCounter(dayCloseJson.RootElement, posCounterFilter);
        var cashFigures = counterFigures ?? StoreDayCloseDashboardReader.ReadTotals(dayCloseJson.RootElement);

        // Prefer computed expected cash from the day-close report (open sessions store expectedCash=0 in payload).
        decimal expectedCash = cashFigures.ExpectedCash;
        using (var reportJson = await _dashboardApi.GetStoreDayCloseReportAsync(businessDate, posCounterFilter, ct))
        {
            var reportExpected = StoreDayCloseDashboardReader.ReadReportExpectedCash(reportJson.RootElement);
            if (reportExpected > 0 || expectedCash <= 0)
                expectedCash = reportExpected;
        }

        using var salesJson = await _dashboardApi.GetStoreSalesAsync("custom", businessDate, businessDate, 1, 1, ct);
        if (!salesJson.RootElement.TryGetProperty("summary", out var summary))
            throw new InvalidOperationException("Central sales summary response missing 'summary'.");

        var netCash = CentralDashboardClient.ReadDecimal(summary, "cashInHand");
        var cardTotal = CentralDashboardClient.ReadDecimal(summary, "cardTotalAmount");
        var upiTotal = CentralDashboardClient.ReadDecimal(summary, "upiTotalAmount");
        var returnCashRefundTotal = CentralDashboardClient.ReadDecimal(summary, "returnCashRefundTotal");
        var creditNoteCashoutTotal = CentralDashboardClient.ReadDecimal(summary, "creditNoteCashoutTotal");
        var cashRefundTotal = returnCashRefundTotal + creditNoteCashoutTotal;
        var dailyExpensesTotal = CentralDashboardClient.ReadDecimal(summary, "dailyExpensesTotal");
        var cashTotal = netCash + cashRefundTotal + dailyExpensesTotal;

        using var movementsJson = await _storePos.ListCashMovementsAsync(businessDate, 200, ct);
        var movementDocs = ExtractDocs(movementsJson, MapCentralMovementToDoc);
        var (depositsTotal, withdrawalsTotal) = DayBillingCloseDocumentReader.SumCashMovementsForBusinessDate(
            movementDocs, businessDate, posCounterFilter);

        using var billsJson = await _storePos.ListBillsAsync(null, 200, ct);
        var billDocs = ExtractDocs(billsJson, BillDocumentService.MapCentralBillToDoc);
        var dayBills = billDocs
            .Where(DayBillingCloseDocumentReader.IsPostedBill)
            .Where(d => DayBillingCloseDocumentReader.MatchesLocalDay(d, localDate))
            .Where(d => DayBillingCloseDocumentReader.MatchesPosCounterFilter(d, posCounterFilter))
            .ToList();

        var invoices = new List<DayCloseInvoiceRow>();
        var stockExceptions = new List<DayCloseStockExceptionRow>();
        foreach (var doc in dayBills)
        {
            DayBillingCloseDocumentReader.TryGetUtcDate(doc, "createdAtUtc", out var sortUtc);
            var postedLocal = sortUtc == default
                ? "—"
                : sortUtc.ToLocalTime().ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture);
            var pos = DayBillingCloseDocumentReader.ReadString(doc, "posCounter") ?? "";
            var dev = DayBillingCloseDocumentReader.ReadString(doc, "deviceId") ?? "";
            var billNo = DayBillingCloseDocumentReader.ReadString(doc, "billNo") ?? "";

            invoices.Add(new DayCloseInvoiceRow
            {
                BillNo = billNo,
                CounterDisplay = CounterDisplayFormatter.Format(pos, dev),
                PostedAtLocal = postedLocal,
                TotalQty = DayBillingCloseDocumentReader.SumBillLineQty(doc),
                Payable = DayBillingCloseDocumentReader.ReadDecimal(doc, "payable"),
                PaymentMode = DayBillingCloseDocumentReader.ReadString(doc, "paymentMode") ?? "",
                SyncStatus = "Synced",
                SortUtc = sortUtc,
            });

            AppendStockExceptionRows(doc, billNo, pos, dev, postedLocal, stockExceptions);
        }
        invoices.Sort((a, b) => b.SortUtc.CompareTo(a.SortUtc));

        using var returnsJson = await _storePos.ListSaleReturnsAsync(null, 200, ct);
        var returnDocs = ExtractDocs(returnsJson, MapCentralReturnToDoc);
        var dayReturns = returnDocs
            .Where(DayBillingCloseDocumentReader.IsPostedReturn)
            .Where(d => DayBillingCloseDocumentReader.MatchesLocalDay(d, localDate))
            .Where(d => DayBillingCloseDocumentReader.MatchesPosCounterFilter(d, posCounterFilter))
            .ToList();

        var returnRows = new List<DayCloseReturnRow>();
        foreach (var doc in dayReturns)
        {
            DayBillingCloseDocumentReader.TryGetUtcDate(doc, "createdAtUtc", out var sortUtc);
            var postedLocal = sortUtc == default
                ? "—"
                : sortUtc.ToLocalTime().ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture);
            var pos = DayBillingCloseDocumentReader.ReadString(doc, "posCounter") ?? "";
            var dev = DayBillingCloseDocumentReader.ReadString(doc, "deviceId") ?? "";

            returnRows.Add(new DayCloseReturnRow
            {
                ReturnNo = DayBillingCloseDocumentReader.ReadString(doc, "returnNo") ?? "",
                OriginalBillNo = DayBillingCloseDocumentReader.ReadString(doc, "originalBillNo") ?? "",
                CounterDisplay = CounterDisplayFormatter.Format(pos, dev),
                PostedAtLocal = postedLocal,
                ReturnTotal = DayBillingCloseDocumentReader.ReadDecimal(doc, "returnTotal"),
                ReturnMode = DayBillingCloseDocumentReader.ReadString(doc, "returnMode") ?? "",
                CreditBalance = DayBillingCloseDocumentReader.ReadDecimal(doc, "creditBalance"),
                CashRefunded = DayBillingCloseDocumentReader.ReadDecimal(doc, "cashRefunded"),
                AmountCollected = DayBillingCloseDocumentReader.ReadDecimal(doc, "amountCollected"),
                PaymentSummary = DayBillingCloseDocumentReader.FormatReturnPaymentSummary(doc),
                SortUtc = sortUtc,
            });
        }
        returnRows.Sort((a, b) => b.SortUtc.CompareTo(a.SortUtc));

        var netCard = cardTotal;
        var netUpi = upiTotal;

        return new DayBillingCloseSnapshot
        {
            LocalDate = localDate.Date,
            BillCount = CentralDashboardClient.ReadInt(summary, "invoices"),
            TotalQty = CentralDashboardClient.ReadDecimal(summary, "itemsSold"),
            TotalAmount = CentralDashboardClient.ReadDecimal(summary, "totalBillAmount"),
            CashTotal = cashTotal,
            CardTotal = cardTotal,
            UpiTotal = upiTotal,
            CreditNoteTotal = CentralDashboardClient.ReadDecimal(summary, "creditAppliedOnBills"),
            ReturnCount = CentralDashboardClient.ReadInt(summary, "returnsCount"),
            ReturnTotalAmount = CentralDashboardClient.ReadDecimal(summary, "returnValue"),
            ReturnCashRefundTotal = returnCashRefundTotal,
            CreditNoteCashoutTotal = creditNoteCashoutTotal,
            CashRefundTotal = cashRefundTotal,
            CreditNoteIssuedTotal = CentralDashboardClient.ReadDecimal(summary, "creditNotesIssuedAmount"),
            NetCashInHand = netCash,
            NetCardInHand = netCard,
            NetUpiInHand = netUpi,
            ActualHandInTotal = netCash + netCard + netUpi,
            DailyExpensesTotal = dailyExpensesTotal,
            DepositsTotal = depositsTotal,
            WithdrawalsTotal = withdrawalsTotal,
            OpeningCash = cashFigures.OpeningCash,
            ExpectedCash = expectedCash,
            ActualCashCounted = cashFigures.ActualCashCounted,
            CashDifference = cashFigures.CashDifference,
            SessionStatus = counterFigures?.Status ?? session?.Status,
            Invoices = invoices,
            Returns = returnRows,
            StockExceptions = stockExceptions,
        };
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

    public async Task<(bool Success, string Message)> ApproveStockExceptionsAsync(
        string storeId,
        string billNo,
        string approvedByUser,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(billNo))
            return (false, "Bill number is required.");

        if (_centralMode?.IsOnlineMode == true)
        {
            if (_storePos == null)
                return (false, "Central store-pos is not configured for online mode.");

            try
            {
                await _storePos.ApproveStockExceptionsAsync(
                    billNo.Trim(),
                    new { approvedBy = approvedByUser.Trim(), approvedByUser = approvedByUser.Trim() },
                    ct);
                return (true, $"Stock decremented for exception line(s) on bill {billNo.Trim()}.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        var billsColl = _db.GetCollection<BsonDocument>("store_bills");
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId),
            Builders<BsonDocument>.Filter.Eq("billNo", billNo.Trim()));

        var doc = await billsColl.Find(filter).FirstOrDefaultAsync(ct);
        if (doc == null)
            return (false, $"Bill {billNo} not found.");

        if (!doc.TryGetValue("stockExceptions", out var exVal) || !exVal.IsBsonArray)
            return (false, "This bill has no stock exceptions.");

        var exceptions = exVal.AsBsonArray;
        var pending = exceptions
            .OfType<BsonDocument>()
            .Where(e => !e.TryGetValue("stockDecremented", out var sd) || !sd.ToBoolean())
            .ToList();

        if (pending.Count == 0)
            return (false, "All stock exceptions on this bill are already approved.");

        var approvedAt = DateTime.UtcNow.ToString("O");
        foreach (var ex in pending)
        {
            var sku = DayBillingCloseDocumentReader.ReadString(ex, "sku") ?? "";
            var requestedQty = DayBillingCloseDocumentReader.ReadDecimal(ex, "requestedQty");
            if (string.IsNullOrWhiteSpace(sku) || requestedQty <= 0)
                continue;

            await _productCatalog.DecrementStockBySkuAsync(
                sku,
                requestedQty,
                reason: "stock_exception_approve",
                billNo: billNo.Trim(),
                actorName: approvedByUser.Trim(),
                ct: ct);

            ex["stockDecremented"] = true;
            ex["approvedAtUtc"] = approvedAt;
            ex["approvedBy"] = approvedByUser.Trim();
        }

        var update = Builders<BsonDocument>.Update.Set("stockExceptions", exceptions);
        await billsColl.UpdateOneAsync(filter, update, cancellationToken: ct);

        if (_auditLog != null)
        {
            await _auditLog.LogEventAsync(new StoreAuditEvent
            {
                EntityType = "bill",
                EntityId = billNo.Trim(),
                Action = "stock_exception_approved",
                ActorName = approvedByUser.Trim(),
                Metadata = new BsonDocument
                {
                    { "approvedLineCount", pending.Count },
                    { "skus", new BsonArray(pending.Select(e => DayBillingCloseDocumentReader.ReadString(e, "sku") ?? "")) },
                },
            }, ct);
        }

        return (true, $"Stock decremented for {pending.Count} exception line(s) on bill {billNo}.");
    }

    private static void AppendStockExceptionRows(
        BsonDocument doc,
        string billNo,
        string pos,
        string dev,
        string postedLocal,
        List<DayCloseStockExceptionRow> target)
    {
        if (!doc.TryGetValue("stockExceptions", out var exVal) || !exVal.IsBsonArray)
            return;

        var counterDisplay = CounterDisplayFormatter.Format(pos, dev);
        foreach (BsonDocument ex in exVal.AsBsonArray.OfType<BsonDocument>())
        {
            if (ex.TryGetValue("stockDecremented", out var sd) && sd.ToBoolean())
                continue;

            target.Add(new DayCloseStockExceptionRow
            {
                BillNo = billNo,
                CounterDisplay = counterDisplay,
                PostedAtLocal = postedLocal,
                Sku = DayBillingCloseDocumentReader.ReadString(ex, "sku") ?? "",
                Description = DayBillingCloseDocumentReader.ReadString(ex, "description") ?? "",
                RequestedQty = DayBillingCloseDocumentReader.ReadDecimal(ex, "requestedQty"),
                AvailableQty = DayBillingCloseDocumentReader.ReadDecimal(ex, "availableQty"),
                CanApprove = true,
            });
        }
    }
}
