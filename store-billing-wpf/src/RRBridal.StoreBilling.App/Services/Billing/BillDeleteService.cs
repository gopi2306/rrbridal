using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Products;
using RRBridal.StoreBilling.App.Services.Store;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Billing;

public sealed class BillDeleteResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";

    public static BillDeleteResult Ok(string message) => new() { Success = true, Message = message };
    public static BillDeleteResult Fail(string message) => new() { Success = false, Message = message };
}

public sealed class BillDeleteService
{
    private readonly IMongoCollection<BsonDocument> _bills;
    private readonly StoreContext _store;
    private readonly BillDocumentService _billDocuments;
    private readonly StoreBillListService _storeBillList;
    private readonly ProductCatalogService _productCatalog;
    private readonly BillingOutboxPublisher _outbox;

    public BillDeleteService(
        IMongoDatabase localDb,
        StoreContext store,
        BillDocumentService billDocuments,
        StoreBillListService storeBillList,
        ProductCatalogService productCatalog,
        BillingOutboxPublisher outbox)
    {
        _bills = localDb.GetCollection<BsonDocument>("store_bills");
        _store = store;
        _billDocuments = billDocuments;
        _storeBillList = storeBillList;
        _productCatalog = productCatalog;
        _outbox = outbox;
    }

    public async Task<BillDeleteResult> DeleteAsync(string billNo, CancellationToken ct = default)
    {
        var trimmed = billNo?.Trim() ?? "";
        if (string.IsNullOrEmpty(trimmed))
            return BillDeleteResult.Fail("Bill number is required.");

        var billDoc = await _billDocuments.GetByBillNoAsync(trimmed, ct);
        if (billDoc == null)
            return BillDeleteResult.Fail($"Bill '{trimmed}' was not found.");

        var status = billDoc.GetValue("status", "posted").AsString;
        if (!string.Equals(status, "posted", StringComparison.OrdinalIgnoreCase))
            return BillDeleteResult.Fail($"Only posted bills can be deleted (status: {status}).");

        var storeId = _store.StoreId;
        var existingReturn = await _storeBillList.GetReturnByBillNoAsync(storeId, trimmed, ct);
        if (existingReturn != null)
            return BillDeleteResult.Fail("Cannot delete a bill that has a return. Remove or reverse the return first.");

        var existingAdjustment = await _storeBillList.GetAdjustmentByBillNoAsync(storeId, trimmed, ct);
        if (existingAdjustment != null)
            return BillDeleteResult.Fail("Cannot delete a bill that has an adjustment.");

        try
        {
            await RestoreStockAsync(billDoc, ct);
            await _outbox.CancelPendingInvoiceCreatedAsync(trimmed, ct);
            await _outbox.PublishInvoiceDeletedAsync(billDoc, ct);

            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("storeId", storeId),
                Builders<BsonDocument>.Filter.Eq("billNo", trimmed));
            var deleted = await _bills.DeleteOneAsync(filter, ct);
            if (deleted.DeletedCount == 0)
                return BillDeleteResult.Fail($"Bill '{trimmed}' could not be removed (it may have already been deleted).");

            return BillDeleteResult.Ok($"Bill {trimmed} deleted. Stock restored and sync queued.");
        }
        catch (Exception ex)
        {
            return BillDeleteResult.Fail("Delete failed: " + ex.Message);
        }
    }

    private async Task RestoreStockAsync(BsonDocument billDoc, CancellationToken ct)
    {
        var skipSkus = ParseUndecrementedExceptionSkus(billDoc);
        if (!billDoc.TryGetValue("lines", out var linesVal) || !linesVal.IsBsonArray)
            return;

        foreach (var item in linesVal.AsBsonArray)
        {
            if (item is not BsonDocument line)
                continue;

            var sku = line.GetValue("sku", "").AsString.Trim();
            if (string.IsNullOrEmpty(sku))
                sku = line.GetValue("productCode", "").AsString.Trim();
            if (string.IsNullOrEmpty(sku))
                continue;

            if (skipSkus.Contains(sku))
                continue;

            var qty = ReadDecimal(line, "qty");
            if (qty <= 0)
                continue;

            var description = line.GetValue("description", "").AsString;
            await _productCatalog.IncrementStockBySkuAsync(sku, qty, description, ct);
        }
    }

    private static HashSet<string> ParseUndecrementedExceptionSkus(BsonDocument billDoc)
    {
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!billDoc.TryGetValue("stockExceptions", out var exVal) || !exVal.IsBsonArray)
            return skip;

        foreach (var item in exVal.AsBsonArray)
        {
            if (item is not BsonDocument row)
                continue;
            if (row.GetValue("stockDecremented", true).ToBoolean())
                continue;
            var sku = row.GetValue("sku", "").AsString.Trim();
            if (!string.IsNullOrEmpty(sku))
                skip.Add(sku);
        }

        return skip;
    }

    private static decimal ReadDecimal(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var v) || v.IsBsonNull)
            return 0;
        try
        {
            return v.IsDecimal128 ? (decimal)v.AsDecimal128 : Convert.ToDecimal(BsonTypeMapper.MapToDotNetValue(v));
        }
        catch
        {
            return 0;
        }
    }
}
