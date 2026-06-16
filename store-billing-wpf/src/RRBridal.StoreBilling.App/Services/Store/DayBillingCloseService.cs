using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Audit;
using RRBridal.StoreBilling.App.Services.Products;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class DayBillingCloseService
{
    private readonly IMongoDatabase _db;
    private readonly ProductCatalogService _productCatalog;
    private readonly StoreAuditLogService? _auditLog;

    public DayBillingCloseService(
        IMongoDatabase localDb,
        ProductCatalogService productCatalog,
        StoreAuditLogService? auditLog = null)
    {
        _db = localDb;
        _productCatalog = productCatalog;
        _auditLog = auditLog;
    }

    public async Task<DayBillingCloseSnapshot> LoadDayCloseAsync(
        string storeId,
        DateTime localDate,
        string? posCounterFilter = null,
        CancellationToken ct = default)
    {
        var billsColl = _db.GetCollection<BsonDocument>("store_bills");
        var returnsColl = _db.GetCollection<BsonDocument>("store_sale_returns");
        var cashoutsColl = _db.GetCollection<BsonDocument>("store_credit_note_cashouts");
        var expensesColl = _db.GetCollection<BsonDocument>("store_daily_expenses");
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

        var netCash = cash - cashRefundTotal + exchangePayments.Cash - dailyExpensesTotal;
        var netCard = card + exchangePayments.Card;
        var netUpi = upi + exchangePayments.Upi;
        var actualHandIn = netCash + netCard + netUpi;

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
            Invoices = invoices,
            Returns = returnRows,
            StockExceptions = stockExceptions,
        };
    }

    public async Task<(bool Success, string Message)> ApproveStockExceptionsAsync(
        string storeId,
        string billNo,
        string approvedByUser,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(billNo))
            return (false, "Bill number is required.");

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
