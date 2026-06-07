using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Products;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class DayBillingCloseService
{
    private readonly IMongoDatabase _db;
    private readonly ProductCatalogService _productCatalog;

    public DayBillingCloseService(IMongoDatabase localDb, ProductCatalogService productCatalog)
    {
        _db = localDb;
        _productCatalog = productCatalog;
    }

    public async Task<DayBillingCloseSnapshot> LoadDayCloseAsync(
        string storeId,
        DateTime localDate,
        string? posCounterFilter = null,
        CancellationToken ct = default)
    {
        var billsColl = _db.GetCollection<BsonDocument>("store_bills");
        var outboxColl = _db.GetCollection<BsonDocument>("outbox_events");

        var storeFilter = Builders<BsonDocument>.Filter.Eq("storeId", storeId);
        var billDocs = await billsColl.Find(storeFilter).ToListAsync(ct);

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
            Invoices = invoices,
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

            await _productCatalog.DecrementStockBySkuAsync(sku, requestedQty, ct);

            ex["stockDecremented"] = true;
            ex["approvedAtUtc"] = approvedAt;
            ex["approvedBy"] = approvedByUser.Trim();
        }

        var update = Builders<BsonDocument>.Update.Set("stockExceptions", exceptions);
        await billsColl.UpdateOneAsync(filter, update, cancellationToken: ct);

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
