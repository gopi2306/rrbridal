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
using RRBridal.StoreBilling.App.Services.Customers;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class StoreBillListService
{
    public const int DefaultLimit = 500;

    private readonly IMongoDatabase _db;
    private CentralOnlineModeService? _centralMode;
    private CentralStorePosClient? _storePos;

    public StoreBillListService(IMongoDatabase localDb)
    {
        _db = localDb;
    }

    public void ConfigureOnline(CentralOnlineModeService centralMode, CentralStorePosClient storePos)
    {
        _centralMode = centralMode;
        _storePos = storePos;
    }

    private bool IsCentralOnline => _centralMode?.IsOnlineMode == true && _storePos != null;

    public async Task<StoreBillListSnapshot> LoadAsync(
        string storeId,
        StoreBillListQuery query,
        CancellationToken ct = default)
    {
        if (IsCentralOnline)
            return await LoadOnlineAsync(query, ct);

        var limit = Math.Clamp(query.Limit, 1, DefaultLimit);
        var billsColl = _db.GetCollection<BsonDocument>("store_bills");
        var storeFilter = Builders<BsonDocument>.Filter.Eq("storeId", storeId);
        var billDocs = await billsColl.Find(storeFilter).ToListAsync(ct);

        var outboxColl = _db.GetCollection<BsonDocument>("outbox_events");
        var outboxEvents = await outboxColl
            .Find(Builders<BsonDocument>.Filter.Eq("storeId", storeId))
            .ToListAsync(ct);
        var outboxByBillNo = DayBillingCloseDocumentReader.BuildOutboxSyncByBillNo(outboxEvents);

        var filtered = billDocs
            .Where(DayBillingCloseDocumentReader.IsPostedBill)
            .Where(d => DayBillingCloseDocumentReader.MatchesPosCounterFilter(d, query.PosCounterFilter))
            .Where(d => MatchesInvoiceNo(d, query.InvoiceNo))
            .Where(d => MatchesCustomerName(d, query.CustomerName))
            .Where(d => MatchesCustomerMobile(d, query.CustomerMobile))
            .Where(d => MatchesSalesmanFilter(d, query))
            .Where(d => MatchesDateFilter(d, query))
            .OrderByDescending(d => GetSortUtc(d))
            .ToList();

        var totalMatched = filtered.Count;
        var truncatedDocs = filtered.Take(limit).ToList();
        var billNos = truncatedDocs
            .Select(d => DayBillingCloseDocumentReader.ReadString(d, "billNo") ?? "")
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .ToList();

        var returnMap = await LoadReturnMapAsync(storeId, billNos, ct);
        var adjustmentMap = await LoadAdjustmentMapAsync(storeId, billNos, ct);

        var rows = truncatedDocs
            .Select(d => MapRow(d, outboxByBillNo, returnMap, adjustmentMap))
            .ToList();

        return new StoreBillListSnapshot
        {
            Rows = rows,
            TotalMatched = totalMatched,
            WasTruncated = totalMatched > limit,
            TotalPayable = rows.Sum(r => r.Payable),
            TotalCash = rows.Sum(r => r.CashAmount),
            TotalCard = rows.Sum(r => r.CardAmount),
            TotalUpi = rows.Sum(r => r.UpiAmount),
            TotalCreditNote = rows.Sum(r => r.CreditNoteAmount),
        };
    }

    private async Task<StoreBillListSnapshot> LoadOnlineAsync(StoreBillListQuery query, CancellationToken ct)
    {
        var limit = Math.Clamp(query.Limit, 1, DefaultLimit);
        var search = !string.IsNullOrWhiteSpace(query.InvoiceNo)
            ? query.InvoiceNo
            : !string.IsNullOrWhiteSpace(query.CustomerName)
                ? query.CustomerName
                : !string.IsNullOrWhiteSpace(query.CustomerMobile)
                    ? query.CustomerMobile
                    : null;

        using var json = await _storePos!.ListBillsAsync(search, Math.Min(200, limit * 3), ct);
        var billDocs = new List<BsonDocument>();
        if (json.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in json.RootElement.EnumerateArray())
                billDocs.Add(BillDocumentService.MapCentralBillToDoc(el));
        }

        var filtered = billDocs
            .Where(DayBillingCloseDocumentReader.IsPostedBill)
            .Where(d => DayBillingCloseDocumentReader.MatchesPosCounterFilter(d, query.PosCounterFilter))
            .Where(d => MatchesInvoiceNo(d, query.InvoiceNo))
            .Where(d => MatchesCustomerName(d, query.CustomerName))
            .Where(d => MatchesCustomerMobile(d, query.CustomerMobile))
            .Where(d => MatchesSalesmanFilter(d, query))
            .Where(d => MatchesDateFilter(d, query))
            .OrderByDescending(d => GetSortUtc(d))
            .ToList();

        var totalMatched = filtered.Count;
        var truncatedDocs = filtered.Take(limit).ToList();
        var billNos = truncatedDocs
            .Select(d => DayBillingCloseDocumentReader.ReadString(d, "billNo") ?? "")
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .ToList();

        // Bills posted while Online are already synced to central by definition.
        var outboxByBillNo = billNos.ToDictionary(b => b, _ => "synced", StringComparer.OrdinalIgnoreCase);
        var returnMap = await LoadReturnMapOnlineAsync(billNos, ct);
        var adjustmentMap = await LoadAdjustmentMapOnlineAsync(billNos, ct);

        var rows = truncatedDocs
            .Select(d => MapRow(d, outboxByBillNo, returnMap, adjustmentMap))
            .ToList();

        return new StoreBillListSnapshot
        {
            Rows = rows,
            TotalMatched = totalMatched,
            WasTruncated = totalMatched > limit,
            TotalPayable = rows.Sum(r => r.Payable),
            TotalCash = rows.Sum(r => r.CashAmount),
            TotalCard = rows.Sum(r => r.CardAmount),
            TotalUpi = rows.Sum(r => r.UpiAmount),
            TotalCreditNote = rows.Sum(r => r.CreditNoteAmount),
        };
    }

    private async Task<Dictionary<string, (bool Has, string No)>> LoadReturnMapOnlineAsync(
        IReadOnlyList<string> billNos,
        CancellationToken ct)
    {
        var map = new Dictionary<string, (bool Has, string No)>(StringComparer.OrdinalIgnoreCase);
        if (billNos.Count == 0)
            return map;

        using var json = await _storePos!.ListSaleReturnsAsync(null, 500, ct);
        if (json.RootElement.ValueKind != JsonValueKind.Array)
            return map;

        foreach (var el in json.RootElement.EnumerateArray())
        {
            var doc = SaleReturnHistoryService.MapCentralReturnToDoc(el);
            if (!IsPostedReturnDoc(doc))
                continue;

            var originalBillNo = DayBillingCloseDocumentReader.ReadString(doc, "originalBillNo") ?? "";
            if (string.IsNullOrWhiteSpace(originalBillNo))
                continue;

            map[originalBillNo] = (true, DayBillingCloseDocumentReader.ReadString(doc, "returnNo") ?? "");
        }

        return map;
    }

    private async Task<Dictionary<string, (bool Has, string No)>> LoadAdjustmentMapOnlineAsync(
        IReadOnlyList<string> billNos,
        CancellationToken ct)
    {
        var map = new Dictionary<string, (bool Has, string No)>(StringComparer.OrdinalIgnoreCase);
        if (billNos.Count == 0)
            return map;

        using var json = await _storePos!.ListAdjustmentsAsync(null, 500, ct);
        if (json.RootElement.ValueKind != JsonValueKind.Array)
            return map;

        foreach (var el in json.RootElement.EnumerateArray())
        {
            var doc = MapCentralAdjustmentToDoc(el);
            var originalBillNo = DayBillingCloseDocumentReader.ReadString(doc, "originalBillNo") ?? "";
            if (string.IsNullOrWhiteSpace(originalBillNo))
                continue;
            var status = DayBillingCloseDocumentReader.ReadString(doc, "status") ?? "posted";
            if (!string.Equals(status, "posted", StringComparison.OrdinalIgnoreCase))
                continue;
            map[originalBillNo] = (true, DayBillingCloseDocumentReader.ReadString(doc, "adjustmentNo") ?? "");
        }

        return map;
    }

    public async Task<BsonDocument?> GetReturnByBillNoAsync(string storeId, string billNo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(billNo))
            return null;

        if (IsCentralOnline)
        {
            using var json = await _storePos!.ListSaleReturnsAsync(billNo.Trim(), 20, ct);
            if (json.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var el in json.RootElement.EnumerateArray())
            {
                var doc = SaleReturnHistoryService.MapCentralReturnToDoc(el);
                if (IsPostedReturnDoc(doc))
                    return doc;
            }

            return null;
        }

        var coll = _db.GetCollection<BsonDocument>("store_sale_returns");
        return await coll.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId),
            Builders<BsonDocument>.Filter.Eq("originalBillNo", billNo.Trim()),
            Builders<BsonDocument>.Filter.Eq("status", "posted"))).FirstOrDefaultAsync(ct);
    }

    public async Task<BsonDocument?> GetReturnByReturnNoAsync(string storeId, string returnNo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(returnNo))
            return null;

        if (IsCentralOnline)
        {
            using var json = await _storePos!.GetSaleReturnAsync(returnNo.Trim(), ct);
            if (json.RootElement.ValueKind == JsonValueKind.Null)
                return null;

            var doc = SaleReturnHistoryService.MapCentralReturnToDoc(json.RootElement);
            return IsPostedReturnDoc(doc) ? doc : null;
        }

        var coll = _db.GetCollection<BsonDocument>("store_sale_returns");
        return await coll.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId),
            Builders<BsonDocument>.Filter.Eq("returnNo", returnNo.Trim()),
            Builders<BsonDocument>.Filter.Eq("status", "posted"))).FirstOrDefaultAsync(ct);
    }

    private static bool IsPostedReturnDoc(BsonDocument doc) =>
        string.Equals(
            DayBillingCloseDocumentReader.ReadString(doc, "status") ?? "posted",
            "posted",
            StringComparison.OrdinalIgnoreCase);

    public async Task<BsonDocument?> GetAdjustmentByBillNoAsync(string storeId, string billNo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(billNo))
            return null;

        if (IsCentralOnline)
        {
            using var json = await _storePos!.GetAdjustmentByOriginalBillAsync(billNo.Trim(), ct);
            if (json.RootElement.ValueKind == JsonValueKind.Null)
                return null;
            return MapCentralAdjustmentToDoc(json.RootElement);
        }

        var coll = _db.GetCollection<BsonDocument>("store_adjustments");
        return await coll.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId),
            Builders<BsonDocument>.Filter.Eq("originalBillNo", billNo.Trim()),
            Builders<BsonDocument>.Filter.Eq("status", "posted"))).FirstOrDefaultAsync(ct);
    }

    private static BsonDocument MapCentralAdjustmentToDoc(JsonElement el)
    {
        BsonDocument doc;
        if (el.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
            doc = BsonDocument.Parse(payload.GetRawText());
        else
            doc = BsonDocument.Parse(el.GetRawText());

        if (el.TryGetProperty("adjustmentNo", out var adjEl) && adjEl.ValueKind == JsonValueKind.String)
            doc["adjustmentNo"] = adjEl.GetString() ?? "";
        if (el.TryGetProperty("storeId", out var storeIdEl) && storeIdEl.ValueKind == JsonValueKind.String)
            doc["storeId"] = storeIdEl.GetString() ?? "";
        if (!doc.Contains("status"))
            doc["status"] = "posted";
        return doc;
    }

    private async Task<Dictionary<string, (bool Has, string No)>> LoadReturnMapAsync(
        string storeId,
        IReadOnlyList<string> billNos,
        CancellationToken ct)
    {
        var map = new Dictionary<string, (bool Has, string No)>(StringComparer.OrdinalIgnoreCase);
        if (billNos.Count == 0)
            return map;

        var coll = _db.GetCollection<BsonDocument>("store_sale_returns");
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId),
            Builders<BsonDocument>.Filter.In("originalBillNo", billNos),
            Builders<BsonDocument>.Filter.Eq("status", "posted"));
        var docs = await coll.Find(filter).ToListAsync(ct);
        foreach (var doc in docs)
        {
            var billNo = DayBillingCloseDocumentReader.ReadString(doc, "originalBillNo") ?? "";
            if (string.IsNullOrWhiteSpace(billNo))
                continue;
            map[billNo] = (true, DayBillingCloseDocumentReader.ReadString(doc, "returnNo") ?? "");
        }

        return map;
    }

    private async Task<Dictionary<string, (bool Has, string No)>> LoadAdjustmentMapAsync(
        string storeId,
        IReadOnlyList<string> billNos,
        CancellationToken ct)
    {
        var map = new Dictionary<string, (bool Has, string No)>(StringComparer.OrdinalIgnoreCase);
        if (billNos.Count == 0)
            return map;

        var coll = _db.GetCollection<BsonDocument>("store_adjustments");
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId),
            Builders<BsonDocument>.Filter.In("originalBillNo", billNos),
            Builders<BsonDocument>.Filter.Eq("status", "posted"));
        var docs = await coll.Find(filter).ToListAsync(ct);
        foreach (var doc in docs)
        {
            var billNo = DayBillingCloseDocumentReader.ReadString(doc, "originalBillNo") ?? "";
            if (string.IsNullOrWhiteSpace(billNo))
                continue;
            map[billNo] = (true, DayBillingCloseDocumentReader.ReadString(doc, "adjustmentNo") ?? "");
        }

        return map;
    }

    private static StoreBillListRow MapRow(
        BsonDocument doc,
        IReadOnlyDictionary<string, string> outboxByBillNo,
        IReadOnlyDictionary<string, (bool Has, string No)> returnMap,
        IReadOnlyDictionary<string, (bool Has, string No)> adjustmentMap)
    {
        var billNo = DayBillingCloseDocumentReader.ReadString(doc, "billNo") ?? "";
        var sortUtc = GetSortUtc(doc);
        var postedLocal = sortUtc == DateTime.MinValue
            ? "—"
            : sortUtc.ToLocalTime().ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture);

        var payments = DayBillingCloseDocumentReader.SumBillPayments(doc);
        returnMap.TryGetValue(billNo, out var ret);
        adjustmentMap.TryGetValue(billNo, out var adj);

        var pos = DayBillingCloseDocumentReader.ReadString(doc, "posCounter") ?? "";
        var dev = DayBillingCloseDocumentReader.ReadString(doc, "deviceId") ?? "";

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
            TotalQty = DayBillingCloseDocumentReader.SumBillLineQty(doc),
            Payable = DayBillingCloseDocumentReader.ReadDecimal(doc, "payable"),
            CashAmount = payments.Cash,
            CardAmount = payments.Card,
            UpiAmount = payments.Upi,
            CreditNoteAmount = payments.CreditNote,
            CreditNoteRefs = DayBillingCloseDocumentReader.FormatBillCreditNoteReferences(doc),
            SyncStatus = DayBillingCloseDocumentReader.ResolveSyncStatus(doc, outboxByBillNo),
            HasReturn = ret.Has,
            ReturnNo = ret.No,
            HasAdjustment = adj.Has,
            AdjustmentNo = adj.No,
            SortUtc = sortUtc,
        };
    }

    private static DateTime GetSortUtc(BsonDocument doc)
    {
        return DayBillingCloseDocumentReader.TryGetUtcDate(doc, "createdAtUtc", out var utc)
            ? utc
            : DateTime.MinValue;
    }

    public static bool MatchesInvoiceNo(BsonDocument doc, string? invoiceNo)
    {
        if (string.IsNullOrWhiteSpace(invoiceNo))
            return true;

        var billNo = DayBillingCloseDocumentReader.ReadString(doc, "billNo") ?? "";
        return billNo.Contains(invoiceNo.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static bool MatchesCustomerName(BsonDocument doc, string? customerName)
    {
        if (string.IsNullOrWhiteSpace(customerName))
            return true;

        var name = DayBillingCloseDocumentReader.ReadString(doc, "customerName") ?? "";
        return name.Contains(customerName.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static bool MatchesCustomerMobile(BsonDocument doc, string? customerMobile)
    {
        if (string.IsNullOrWhiteSpace(customerMobile))
            return true;

        var phone = DayBillingCloseDocumentReader.ReadString(doc, "customerPhone") ?? "";
        return PhoneMatchHelper.PhoneMatches(phone, customerMobile);
    }

    public static bool MatchesSalesmanFilter(BsonDocument doc, StoreBillListQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.SalesmanGroupKey))
            return SalesmanSalesAggregationService.MatchesSalesmanGroupKey(doc, query.SalesmanGroupKey);

        if (!string.IsNullOrWhiteSpace(query.SalesmanId))
        {
            var id = DayBillingCloseDocumentReader.ReadString(doc, "salesmanId") ?? "";
            if (!string.Equals(id, query.SalesmanId.Trim(), StringComparison.Ordinal))
                return false;
        }

        if (!string.IsNullOrWhiteSpace(query.SalesmanCode))
        {
            var code = DayBillingCloseDocumentReader.ReadString(doc, "salesmanCode") ?? "";
            var name = DayBillingCloseDocumentReader.ReadString(doc, "salesman") ?? "";
            var filter = query.SalesmanCode.Trim();
            if (string.Equals(filter, "__legacy__", StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(code);
            if (!string.Equals(code, filter, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, filter, StringComparison.OrdinalIgnoreCase)
                && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public static bool MatchesDateFilter(BsonDocument doc, StoreBillListQuery query)
    {
        if (!DayBillingCloseDocumentReader.TryGetUtcDate(doc, "createdAtUtc", out var utc))
            return query.BusinessDate == null && !query.UseDateRange;

        var localDate = utc.ToLocalTime().Date;

        if (query.UseDateRange)
        {
            if (query.DateFrom.HasValue && localDate < query.DateFrom.Value.Date)
                return false;
            if (query.DateTo.HasValue && localDate > query.DateTo.Value.Date)
                return false;
            return true;
        }

        if (query.BusinessDate.HasValue)
            return localDate == query.BusinessDate.Value.Date;

        return true;
    }
}
