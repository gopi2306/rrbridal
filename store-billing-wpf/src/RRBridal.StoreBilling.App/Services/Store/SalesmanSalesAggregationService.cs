using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class SalesmanSalesSummaryRow
{
    public required string GroupKey { get; init; }
    public required string SalesmanCode { get; init; }
    public required string SalesmanName { get; init; }
    public required string SalesmanId { get; init; }
    public int BillCount { get; init; }
    public decimal TotalQty { get; init; }
    public decimal TotalPayable { get; init; }
    public decimal TotalCash { get; init; }
    public string TotalQtyFormatted => TotalQty.ToString("N2", CultureInfo.InvariantCulture);
    public string AvgPayableFormatted => BillCount > 0
        ? MoneyMath.FormatRupee(TotalPayable / BillCount)
        : MoneyMath.FormatRupee(0);
    public string TotalCashFormatted => MoneyMath.FormatRupee(TotalCash);
    public string TotalPayableFormatted => MoneyMath.FormatRupee(TotalPayable);
    public string DisplayLabel => string.IsNullOrWhiteSpace(SalesmanCode)
        ? string.IsNullOrWhiteSpace(SalesmanName) ? "(No salesman)" : SalesmanName
        : $"{SalesmanCode} — {SalesmanName}";
}

public sealed class SalesmanSalesAggregationService
{
    private readonly IMongoDatabase _db;

    public SalesmanSalesAggregationService(IMongoDatabase localDb)
    {
        _db = localDb;
    }

    public async Task<IReadOnlyList<SalesmanSalesSummaryRow>> AggregateAsync(
        string storeId,
        string? posCounterFilter,
        DateTime? businessDate,
        bool useDateRange,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken ct = default)
    {
        var query = new StoreBillListQuery
        {
            PosCounterFilter = posCounterFilter,
            BusinessDate = businessDate,
            UseDateRange = useDateRange,
            DateFrom = dateFrom,
            DateTo = dateTo,
            Limit = StoreBillListService.DefaultLimit,
        };

        var listService = new StoreBillListService(_db);
        var snap = await listService.LoadAsync(storeId, query, ct);

        var billsColl = _db.GetCollection<BsonDocument>("store_bills");
        var billDocs = await billsColl.Find(Builders<BsonDocument>.Filter.Eq("storeId", storeId)).ToListAsync(ct);
        var filteredBillNos = snap.Rows.Select(r => r.BillNo).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var docsByBillNo = billDocs
            .Where(DayBillingCloseDocumentReader.IsPostedBill)
            .Where(d => filteredBillNos.Contains(DayBillingCloseDocumentReader.ReadString(d, "billNo") ?? ""))
            .ToDictionary(d => DayBillingCloseDocumentReader.ReadString(d, "billNo") ?? "", StringComparer.OrdinalIgnoreCase);

        var groups = new Dictionary<string, (string Code, string Name, string Id, int Bills, decimal Qty, decimal Payable, decimal Cash)>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in snap.Rows)
        {
            if (!docsByBillNo.TryGetValue(row.BillNo, out var doc))
                continue;

            var code = DayBillingCloseDocumentReader.ReadString(doc, "salesmanCode") ?? "";
            var name = DayBillingCloseDocumentReader.ReadString(doc, "salesman") ?? "";
            var id = DayBillingCloseDocumentReader.ReadString(doc, "salesmanId") ?? "";
            var key = ResolveSalesmanGroupKey(id, code, name);

            if (!groups.TryGetValue(key, out var agg))
            {
                agg = (code, name, id, 0, 0m, 0m, 0m);
            }

            agg.Bills += 1;
            agg.Qty += DayBillingCloseDocumentReader.SumBillLineQty(doc);
            agg.Payable += DayBillingCloseDocumentReader.ReadDecimal(doc, "payable");
            agg.Cash += row.CashAmount;
            groups[key] = agg;
        }

        return groups
            .Select(kv => new SalesmanSalesSummaryRow
            {
                GroupKey = kv.Key,
                SalesmanCode = kv.Value.Code,
                SalesmanName = kv.Key == "__legacy__" ? "(Legacy bills)" : kv.Value.Name,
                SalesmanId = kv.Value.Id,
                BillCount = kv.Value.Bills,
                TotalQty = kv.Value.Qty,
                TotalPayable = kv.Value.Payable,
                TotalCash = kv.Value.Cash,
            })
            .OrderByDescending(r => r.TotalPayable)
            .ThenBy(r => r.SalesmanName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string ResolveSalesmanGroupKey(string id, string code, string name)
    {
        if (!string.IsNullOrWhiteSpace(id))
            return $"id:{id.Trim()}";
        if (!string.IsNullOrWhiteSpace(code))
            return $"code:{code.Trim()}";
        if (!string.IsNullOrWhiteSpace(name))
            return $"name:{name.Trim()}";
        return "__legacy__";
    }

    public static bool MatchesSalesmanGroupKey(BsonDocument doc, string? groupKey)
    {
        if (string.IsNullOrWhiteSpace(groupKey))
            return true;

        var id = DayBillingCloseDocumentReader.ReadString(doc, "salesmanId") ?? "";
        var code = DayBillingCloseDocumentReader.ReadString(doc, "salesmanCode") ?? "";
        var name = DayBillingCloseDocumentReader.ReadString(doc, "salesman") ?? "";
        var docKey = ResolveSalesmanGroupKey(id, code, name);
        return string.Equals(docKey, groupKey.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
