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

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class BillMarginAggregationService
{
    public const int DefaultLimit = 500;

    private readonly IMongoDatabase _db;
    private CentralOnlineModeService? _centralMode;
    private CentralDashboardClient? _dashboardApi;

    public BillMarginAggregationService(IMongoDatabase localDb)
    {
        _db = localDb;
    }

    public void ConfigureOnline(CentralOnlineModeService centralMode, CentralDashboardClient dashboardApi)
    {
        _centralMode = centralMode;
        _dashboardApi = dashboardApi;
    }

    private bool IsCentralOnline => _centralMode?.IsOnlineMode == true && _dashboardApi != null;

    public async Task<BillMarginSnapshot> LoadAsync(
        string storeId,
        BillMarginQuery query,
        CancellationToken ct = default)
    {
        if (IsCentralOnline)
            return await LoadOnlineAsync(query, ct);

        var limit = Math.Clamp(query.Limit, 1, DefaultLimit);
        var billsColl = _db.GetCollection<BsonDocument>("store_bills");
        var storeFilter = Builders<BsonDocument>.Filter.Eq("storeId", storeId);
        var billDocs = await billsColl.Find(storeFilter).ToListAsync(ct);

        var listQuery = new StoreBillListQuery
        {
            InvoiceNo = query.InvoiceNo,
            BusinessDate = query.BusinessDate,
            UseDateRange = query.UseDateRange,
            DateFrom = query.DateFrom,
            DateTo = query.DateTo,
            PosCounterFilter = query.PosCounterFilter,
            SalesmanGroupKey = query.SalesmanGroupKey,
            SalesmanCode = query.SalesmanCode,
            Limit = limit,
        };

        var filtered = billDocs
            .Where(DayBillingCloseDocumentReader.IsPostedBill)
            .Where(d => DayBillingCloseDocumentReader.MatchesPosCounterFilter(d, query.PosCounterFilter))
            .Where(d => StoreBillListService.MatchesInvoiceNo(d, query.InvoiceNo))
            .Where(d => StoreBillListService.MatchesSalesmanFilter(d, listQuery))
            .Where(d => StoreBillListService.MatchesDateFilter(d, listQuery))
            .OrderByDescending(d => GetSortUtc(d))
            .ToList();

        var totalMatched = filtered.Count;
        var truncatedDocs = filtered.Take(limit).ToList();
        var billNos = truncatedDocs
            .Select(d => DayBillingCloseDocumentReader.ReadString(d, "billNo") ?? "")
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .ToList();

        var returnDocs = await LoadReturnDocsAsync(storeId, billNos, ct);
        var adjustmentDocs = await LoadAdjustmentDocsAsync(storeId, billNos, ct);

        var rows = truncatedDocs
            .Select(d => MapRow(d, returnDocs, adjustmentDocs))
            .ToList();

        var totalCost = rows.Sum(r => r.CostPrice);
        var totalSelling = rows.Sum(r => r.SellingPrice);
        var totalDiscount = rows.Sum(r => r.Discount);
        var totalMargin = rows.Sum(r => r.MarginAmount);

        return new BillMarginSnapshot
        {
            Rows = rows,
            TotalMatched = totalMatched,
            WasTruncated = totalMatched > limit,
            TotalCost = totalCost,
            TotalSelling = totalSelling,
            TotalDiscount = totalDiscount,
            TotalMargin = totalMargin,
            TotalMarginPercent = ComputeMarginPercent(totalMargin, totalCost),
        };
    }

    private async Task<BillMarginSnapshot> LoadOnlineAsync(BillMarginQuery query, CancellationToken ct)
    {
        var limit = Math.Clamp(query.Limit, 1, DefaultLimit);
        var (period, from, to) = OnlineSalesPeriodMapper.Resolve(
            query.BusinessDate, query.UseDateRange, query.DateFrom, query.DateTo);

        using var json = await _dashboardApi!.GetBillMarginAsync(period, from, to, ct);
        var root = json.RootElement;
        if (!root.TryGetProperty("rows", out var rowsEl) || rowsEl.ValueKind != JsonValueKind.Array)
        {
            return new BillMarginSnapshot
            {
                Rows = Array.Empty<BillMarginRow>(),
            };
        }

        var allRows = new List<(BillMarginRow Row, string PosCounter)>();
        foreach (var el in rowsEl.EnumerateArray())
            allRows.Add(MapOnlineRow(el));

        IEnumerable<(BillMarginRow Row, string PosCounter)> filtered = allRows;
        if (!string.IsNullOrWhiteSpace(query.PosCounterFilter))
        {
            var pos = query.PosCounterFilter.Trim();
            filtered = filtered.Where(r => string.Equals(r.PosCounter, pos, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.InvoiceNo))
        {
            var q = query.InvoiceNo.Trim();
            filtered = filtered.Where(r => r.Row.BillNo.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.SalesmanGroupKey))
        {
            if (query.SalesmanGroupKey.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Salesman-ID based filtering is not available for bill margin in Online mode.");

            var key = query.SalesmanGroupKey.Trim();
            filtered = filtered.Where(r => MatchesOnlineSalesmanGroupKey(r.Row, key));
        }
        else if (!string.IsNullOrWhiteSpace(query.SalesmanCode))
        {
            var code = query.SalesmanCode.Trim();
            filtered = filtered.Where(r =>
                string.Equals(r.Row.SalesmanCode, code, StringComparison.OrdinalIgnoreCase)
                || string.Equals(r.Row.SalesmanName, code, StringComparison.OrdinalIgnoreCase)
                || r.Row.SalesmanName.Contains(code, StringComparison.OrdinalIgnoreCase));
        }

        var orderedRows = filtered.Select(r => r.Row).OrderByDescending(r => r.SortUtc).ToList();
        var totalMatched = orderedRows.Count;
        var rows = orderedRows.Take(limit).ToList();

        var totalCost = rows.Sum(r => r.CostPrice);
        var totalSelling = rows.Sum(r => r.SellingPrice);
        var totalDiscount = rows.Sum(r => r.Discount);
        var totalMargin = rows.Sum(r => r.MarginAmount);

        return new BillMarginSnapshot
        {
            Rows = rows,
            TotalMatched = totalMatched,
            WasTruncated = totalMatched > limit,
            TotalCost = totalCost,
            TotalSelling = totalSelling,
            TotalDiscount = totalDiscount,
            TotalMargin = totalMargin,
            TotalMarginPercent = ComputeMarginPercent(totalMargin, totalCost),
        };
    }

    private static bool MatchesOnlineSalesmanGroupKey(BillMarginRow row, string groupKey)
    {
        if (string.Equals(groupKey, "__legacy__", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(row.SalesmanCode) && string.IsNullOrWhiteSpace(row.SalesmanName);

        if (groupKey.StartsWith("code:", StringComparison.OrdinalIgnoreCase))
        {
            var code = groupKey["code:".Length..];
            return string.Equals(row.SalesmanCode, code, StringComparison.OrdinalIgnoreCase);
        }

        if (groupKey.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
        {
            var name = groupKey["name:".Length..];
            return string.Equals(row.SalesmanName, name, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static (BillMarginRow Row, string PosCounter) MapOnlineRow(JsonElement el)
    {
        var postedAtRaw = CentralDashboardClient.ReadString(el, "postedAt");
        var sortUtc = DateTime.MinValue;
        if (!string.IsNullOrWhiteSpace(postedAtRaw)
            && DateTime.TryParse(postedAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            sortUtc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();

        var postedLocal = sortUtc == DateTime.MinValue
            ? "—"
            : sortUtc.ToLocalTime().ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture);

        var pos = CentralDashboardClient.ReadString(el, "posCounter");
        var costPrice = CentralDashboardClient.ReadDecimal(el, "costPrice");
        var sellingPrice = CentralDashboardClient.ReadDecimal(el, "sellingPrice");
        var marginAmount = CentralDashboardClient.ReadDecimal(el, "marginAmount");

        var row = new BillMarginRow
        {
            BillNo = CentralDashboardClient.ReadString(el, "billNo"),
            BillDate = CentralDashboardClient.ReadString(el, "billDate"),
            CustomerName = CentralDashboardClient.ReadString(el, "customerName"),
            SalesmanCode = CentralDashboardClient.ReadString(el, "salesmanCode"),
            SalesmanName = CentralDashboardClient.ReadString(el, "salesmanName"),
            CounterDisplay = CounterDisplayFormatter.Format(pos, ""),
            PostedAtLocal = postedLocal,
            TotalQty = CentralDashboardClient.ReadDecimal(el, "qty"),
            CostPrice = costPrice,
            SellingPrice = sellingPrice,
            Discount = CentralDashboardClient.ReadDecimal(el, "discount"),
            MarginAmount = marginAmount,
            MarginPercent = CentralDashboardClient.ReadDecimal(el, "marginPercentage"),
            HasReturn = ReadBool(el, "hasReturn"),
            ReturnNo = CentralDashboardClient.ReadString(el, "returnNo"),
            HasAdjustment = ReadBool(el, "hasAdjustment"),
            AdjustmentNo = CentralDashboardClient.ReadString(el, "adjustmentNo"),
            SortUtc = sortUtc,
        };

        return (row, pos);
    }

    private static bool ReadBool(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return false;
        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(p.GetString(), out var b) && b,
            _ => false,
        };
    }

    private async Task<Dictionary<string, BsonDocument>> LoadReturnDocsAsync(
        string storeId,
        IReadOnlyList<string> billNos,
        CancellationToken ct)
    {
        var map = new Dictionary<string, BsonDocument>(StringComparer.OrdinalIgnoreCase);
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
            map[billNo] = doc;
        }

        return map;
    }

    private async Task<Dictionary<string, BsonDocument>> LoadAdjustmentDocsAsync(
        string storeId,
        IReadOnlyList<string> billNos,
        CancellationToken ct)
    {
        var map = new Dictionary<string, BsonDocument>(StringComparer.OrdinalIgnoreCase);
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
            map[billNo] = doc;
        }

        return map;
    }

    private static BillMarginRow MapRow(
        BsonDocument billDoc,
        IReadOnlyDictionary<string, BsonDocument> returnDocs,
        IReadOnlyDictionary<string, BsonDocument> adjustmentDocs)
    {
        var billNo = DayBillingCloseDocumentReader.ReadString(billDoc, "billNo") ?? "";
        var sortUtc = GetSortUtc(billDoc);
        var postedLocal = sortUtc == DateTime.MinValue
            ? "—"
            : sortUtc.ToLocalTime().ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture);

        returnDocs.TryGetValue(billNo, out var returnDoc);
        adjustmentDocs.TryGetValue(billNo, out var adjustmentDoc);

        var billLines = ReadBillLines(billDoc);
        var effective = ApplyAdjustments(billLines, adjustmentDoc);
        var totals = SumLines(effective);

        if (returnDoc != null)
        {
            var returnTotals = AggregateReturnLines(returnDoc, billLines);
            totals = new LineMarginTotals(
                Qty: totals.Qty - returnTotals.Qty,
                Cost: totals.Cost - returnTotals.Cost,
                Selling: totals.Selling - returnTotals.Selling,
                Discount: totals.Discount - returnTotals.Discount);
        }

        var marginAmount = MoneyMath.RoundAmount(totals.Selling - totals.Cost);
        var marginPercent = ComputeMarginPercent(marginAmount, totals.Cost);

        var pos = DayBillingCloseDocumentReader.ReadString(billDoc, "posCounter") ?? "";
        var dev = DayBillingCloseDocumentReader.ReadString(billDoc, "deviceId") ?? "";

        return new BillMarginRow
        {
            BillNo = billNo,
            BillDate = DayBillingCloseDocumentReader.ReadString(billDoc, "billDate") ?? "",
            CustomerName = DayBillingCloseDocumentReader.ReadString(billDoc, "customerName") ?? "",
            SalesmanCode = DayBillingCloseDocumentReader.ReadString(billDoc, "salesmanCode") ?? "",
            SalesmanName = DayBillingCloseDocumentReader.ReadString(billDoc, "salesman") ?? "",
            CounterDisplay = CounterDisplayFormatter.Format(pos, dev),
            PostedAtLocal = postedLocal,
            TotalQty = totals.Qty,
            CostPrice = MoneyMath.RoundAmount(totals.Cost),
            SellingPrice = MoneyMath.RoundAmount(totals.Selling),
            Discount = MoneyMath.RoundAmount(totals.Discount),
            MarginAmount = marginAmount,
            MarginPercent = marginPercent,
            HasReturn = returnDoc != null,
            ReturnNo = returnDoc != null
                ? DayBillingCloseDocumentReader.ReadString(returnDoc, "returnNo") ?? ""
                : "",
            HasAdjustment = adjustmentDoc != null,
            AdjustmentNo = adjustmentDoc != null
                ? DayBillingCloseDocumentReader.ReadString(adjustmentDoc, "adjustmentNo") ?? ""
                : "",
            SortUtc = sortUtc,
        };
    }

    private static List<BillLineMargin> ReadBillLines(BsonDocument billDoc)
    {
        var result = new List<BillLineMargin>();
        if (!billDoc.Contains("lines") || !billDoc["lines"].IsBsonArray)
            return result;

        foreach (var item in billDoc["lines"].AsBsonArray)
        {
            if (item is not BsonDocument line)
                continue;

            var qty = DayBillingCloseDocumentReader.ReadDecimal(line, "qty");
            if (qty <= 0)
                continue;

            var taxPercent = DayBillingCloseDocumentReader.ReadDecimal(line, "taxPercent");
            var isIgst = line.Contains("igstPercent")
                && DayBillingCloseDocumentReader.ReadDecimal(line, "igstPercent") > 0;
            if (!isIgst && line.Contains("igstAmount")
                && DayBillingCloseDocumentReader.ReadDecimal(line, "igstAmount") > 0)
                isIgst = true;

            var amount = DayBillingCloseDocumentReader.ReadDecimal(line, "amount");
            var originalTaxable = BillingDiscountCalculator
                .ReverseSplitFromInclusive(amount, taxPercent, isIgst).Taxable;

            var selling = DayBillingCloseDocumentReader.ReadDecimal(line, "revisedAmount");
            if (selling <= 0)
            {
                var scheme = DayBillingCloseDocumentReader.ReadDecimal(line, "schemeDiscountAmount");
                var itemDisc = DayBillingCloseDocumentReader.ReadDecimal(line, "discountAmount");
                var cash = DayBillingCloseDocumentReader.ReadDecimal(line, "cashDiscountAmount");
                selling = BillingDiscountCalculator
                    .ComputeRevisedFromInclusiveDiscounts(amount, scheme, itemDisc, cash, taxPercent, isIgst)
                    .Taxable;
            }

            var discount = MoneyMath.RoundAmount(Math.Max(0m, originalTaxable - selling));
            var costPrice = DayBillingCloseDocumentReader.ReadDecimal(line, "costPrice");
            var lineNo = line.Contains("lineNo") && line["lineNo"].IsNumeric
                ? line["lineNo"].ToInt32()
                : 0;
            var sku = DayBillingCloseDocumentReader.ReadString(line, "sku")
                      ?? DayBillingCloseDocumentReader.ReadString(line, "productCode")
                      ?? "";

            result.Add(new BillLineMargin(
                LineNo: lineNo,
                Sku: sku,
                Qty: qty,
                CostUnit: costPrice,
                Cost: MoneyMath.RoundAmount(costPrice * qty),
                Selling: selling,
                Discount: discount,
                OriginalTaxable: originalTaxable,
                TaxPercent: taxPercent,
                IsIgst: isIgst));
        }

        return result;
    }

    private static List<BillLineMargin> ApplyAdjustments(
        IReadOnlyList<BillLineMargin> billLines,
        BsonDocument? adjustmentDoc)
    {
        if (adjustmentDoc == null || !adjustmentDoc.Contains("lines") || !adjustmentDoc["lines"].IsBsonArray)
            return billLines.ToList();

        var byLineNo = billLines
            .Where(l => l.LineNo > 0)
            .GroupBy(l => l.LineNo)
            .ToDictionary(g => g.Key, g => g.First());
        var bySku = billLines
            .Where(l => !string.IsNullOrWhiteSpace(l.Sku))
            .GroupBy(l => l.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var overridden = new HashSet<int>();
        var overriddenSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<BillLineMargin>();

        foreach (var item in adjustmentDoc["lines"].AsBsonArray)
        {
            if (item is not BsonDocument adjLine)
                continue;

            var lineNo = adjLine.Contains("lineNo") && adjLine["lineNo"].IsNumeric
                ? adjLine["lineNo"].ToInt32()
                : 0;
            var sku = DayBillingCloseDocumentReader.ReadString(adjLine, "sku") ?? "";

            BillLineMargin? original = null;
            if (lineNo > 0 && byLineNo.TryGetValue(lineNo, out var byNo))
                original = byNo;
            else if (!string.IsNullOrWhiteSpace(sku) && bySku.TryGetValue(sku, out var byCode))
                original = byCode;

            if (original == null)
                continue;

            var src = original.Value;
            var adjustedQty = DayBillingCloseDocumentReader.ReadDecimal(adjLine, "adjustedQty");
            var adjustedAmount = DayBillingCloseDocumentReader.ReadDecimal(adjLine, "adjustedAmount");
            var taxPercent = DayBillingCloseDocumentReader.ReadDecimal(adjLine, "taxPercent");
            if (taxPercent <= 0)
                taxPercent = src.TaxPercent;

            // Adjustment payable math treats adjustedAmount as taxable (ex GST).
            var adjustedTaxable = adjustedAmount;
            var discount = MoneyMath.RoundAmount(
                Math.Max(0m, src.OriginalTaxable - adjustedTaxable));

            result.Add(src with
            {
                Qty = adjustedQty,
                Cost = MoneyMath.RoundAmount(src.CostUnit * adjustedQty),
                Selling = MoneyMath.RoundAmount(adjustedTaxable),
                Discount = discount,
            });

            if (src.LineNo > 0)
                overridden.Add(src.LineNo);
            if (!string.IsNullOrWhiteSpace(src.Sku))
                overriddenSkus.Add(src.Sku);
        }

        foreach (var line in billLines)
        {
            if (line.LineNo > 0 && overridden.Contains(line.LineNo))
                continue;
            if (!string.IsNullOrWhiteSpace(line.Sku) && overriddenSkus.Contains(line.Sku))
                continue;
            result.Add(line);
        }

        return result;
    }

    private static LineMarginTotals AggregateReturnLines(
        BsonDocument returnDoc,
        IReadOnlyList<BillLineMargin> originalBillLines)
    {
        var byLineNo = originalBillLines
            .Where(l => l.LineNo > 0)
            .GroupBy(l => l.LineNo)
            .ToDictionary(g => g.Key, g => g.First());
        var bySku = originalBillLines
            .Where(l => !string.IsNullOrWhiteSpace(l.Sku))
            .GroupBy(l => l.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        BsonArray? lines = null;
        if (returnDoc.Contains("returnLines") && returnDoc["returnLines"].IsBsonArray)
            lines = returnDoc["returnLines"].AsBsonArray;
        else if (returnDoc.Contains("lines") && returnDoc["lines"].IsBsonArray)
            lines = returnDoc["lines"].AsBsonArray;

        if (lines == null)
            return default;

        decimal qty = 0, cost = 0, selling = 0, discount = 0;
        foreach (var item in lines)
        {
            if (item is not BsonDocument line)
                continue;

            var returnQty = DayBillingCloseDocumentReader.ReadDecimal(line, "returnQty");
            if (returnQty <= 0)
                returnQty = DayBillingCloseDocumentReader.ReadDecimal(line, "qty");
            if (returnQty <= 0)
                continue;

            var lineNo = line.Contains("lineNo") && line["lineNo"].IsNumeric
                ? line["lineNo"].ToInt32()
                : 0;
            var sku = DayBillingCloseDocumentReader.ReadString(line, "sku")
                      ?? DayBillingCloseDocumentReader.ReadString(line, "productCode")
                      ?? "";

            BillLineMargin? original = null;
            if (lineNo > 0 && byLineNo.TryGetValue(lineNo, out var byNo))
                original = byNo;
            else if (!string.IsNullOrWhiteSpace(sku) && bySku.TryGetValue(sku, out var byCode))
                original = byCode;

            var costUnit = original?.CostUnit
                           ?? DayBillingCloseDocumentReader.ReadDecimal(line, "costPrice");
            cost += MoneyMath.RoundAmount(costUnit * returnQty);
            qty += returnQty;

            var taxPercent = DayBillingCloseDocumentReader.ReadDecimal(line, "taxPercent");
            if (taxPercent <= 0 && original.HasValue)
                taxPercent = original.Value.TaxPercent;
            var isIgst = original?.IsIgst ?? false;

            // Sale return stores amount as taxable (ex GST). Prefer that.
            var taxable = DayBillingCloseDocumentReader.ReadDecimal(line, "amount");
            if (taxable <= 0)
                taxable = DayBillingCloseDocumentReader.ReadDecimal(line, "revisedAmount");
            if (taxable <= 0)
            {
                var inclusive = DayBillingCloseDocumentReader.ReadDecimal(line, "revisedInclusiveAmount");
                if (inclusive <= 0)
                    inclusive = DayBillingCloseDocumentReader.ReadDecimal(line, "lineTotal");
                taxable = BillingDiscountCalculator
                    .ReverseSplitFromInclusive(inclusive, taxPercent, isIgst).Taxable;
            }

            selling += taxable;

            var gross = DayBillingCloseDocumentReader.ReadDecimal(line, "grossAmount");
            if (gross > 0)
            {
                var grossTaxable = BillingDiscountCalculator
                    .ReverseSplitFromInclusive(gross, taxPercent, isIgst).Taxable;
                discount += MoneyMath.RoundAmount(Math.Max(0m, grossTaxable - taxable));
            }
            else
            {
                var itemDisc = DayBillingCloseDocumentReader.ReadDecimal(line, "discountAmount");
                var cashDisc = DayBillingCloseDocumentReader.ReadDecimal(line, "cashDiscountAmount");
                var discInclusive = itemDisc + cashDisc;
                if (discInclusive > 0)
                {
                    discount += BillingDiscountCalculator
                        .ReverseSplitFromInclusive(discInclusive, taxPercent, isIgst).Taxable;
                }
            }
        }

        return new LineMarginTotals(qty, cost, selling, discount);
    }

    private static LineMarginTotals SumLines(IEnumerable<BillLineMargin> lines)
    {
        decimal qty = 0, cost = 0, selling = 0, discount = 0;
        foreach (var line in lines)
        {
            qty += line.Qty;
            cost += line.Cost;
            selling += line.Selling;
            discount += line.Discount;
        }

        return new LineMarginTotals(qty, cost, selling, discount);
    }

    private static decimal ComputeMarginPercent(decimal marginAmount, decimal cost)
    {
        if (cost <= 0)
            return 0;
        return MoneyMath.RoundAmount(marginAmount / cost * 100m);
    }

    private static DateTime GetSortUtc(BsonDocument doc) =>
        DayBillingCloseDocumentReader.TryGetUtcDate(doc, "createdAtUtc", out var utc)
            ? utc
            : DateTime.MinValue;

    private readonly record struct BillLineMargin(
        int LineNo,
        string Sku,
        decimal Qty,
        decimal CostUnit,
        decimal Cost,
        decimal Selling,
        decimal Discount,
        decimal OriginalTaxable,
        decimal TaxPercent,
        bool IsIgst);

    private readonly record struct LineMarginTotals(
        decimal Qty,
        decimal Cost,
        decimal Selling,
        decimal Discount);
}
