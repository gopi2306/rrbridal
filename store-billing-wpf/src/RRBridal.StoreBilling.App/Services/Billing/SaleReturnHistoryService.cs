using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Api;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Billing;

public sealed class SaleReturnHistoryService
{
    private readonly IMongoCollection<BsonDocument> _returns;
    private CentralOnlineModeService? _centralMode;
    private CentralStorePosClient? _storePos;

    public SaleReturnHistoryService(IMongoDatabase localDb)
    {
        _returns = localDb.GetCollection<BsonDocument>("store_sale_returns");
    }

    public void ConfigureOnline(CentralOnlineModeService centralMode, CentralStorePosClient storePos)
    {
        _centralMode = centralMode;
        _storePos = storePos;
    }

    private bool IsCentralOnline => _centralMode?.IsOnlineMode == true && _storePos != null;

    public async Task<BsonDocument?> FindFirstPostedReturnForBillAsync(
        string storeId,
        string billNo,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(billNo))
            return null;

        var all = await FindAllPostedReturnsForBillAsync(storeId, billNo, ct);
        return all.FirstOrDefault();
    }

    public async Task<IReadOnlyList<BsonDocument>> FindAllPostedReturnsForBillAsync(
        string storeId,
        string billNo,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(billNo))
            return Array.Empty<BsonDocument>();

        if (IsCentralOnline)
        {
            var docs = await ListCentralReturnsAsync(billNo.Trim(), ct);
            return docs.Where(IsPostedNonLegacy).ToList();
        }

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId?.Trim() ?? ""),
            Builders<BsonDocument>.Filter.Eq("originalBillNo", billNo.Trim()),
            Builders<BsonDocument>.Filter.Eq("status", "posted"),
            Builders<BsonDocument>.Filter.Ne("isLegacy", true));

        return await _returns.Find(filter).ToListAsync(ct);
    }

    public static bool IsLegacyReturn(BsonDocument doc) =>
        doc.Contains("isLegacy") && doc["isLegacy"].IsBoolean && doc["isLegacy"].AsBoolean;

    public async Task<int> CountLegacyReturnsForReferenceAsync(
        string storeId,
        string referenceBillNo,
        string? customerPhoneNorm,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(referenceBillNo))
            return 0;

        if (IsCentralOnline)
        {
            var docs = await ListCentralReturnsAsync(referenceBillNo.Trim(), ct);
            IEnumerable<BsonDocument> query = docs.Where(d =>
                IsLegacyReturn(d) && string.Equals(ReadStatus(d), "posted", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(customerPhoneNorm))
            {
                var pattern = new Regex($"^{Regex.Escape(customerPhoneNorm.Trim())}", RegexOptions.IgnoreCase);
                query = query.Where(d => pattern.IsMatch(ReadString(d, "customerPhone") ?? ""));
            }

            return query.Count();
        }

        var filters = new List<FilterDefinition<BsonDocument>>
        {
            Builders<BsonDocument>.Filter.Eq("storeId", storeId?.Trim() ?? ""),
            Builders<BsonDocument>.Filter.Eq("originalBillNo", referenceBillNo.Trim()),
            Builders<BsonDocument>.Filter.Eq("status", "posted"),
            Builders<BsonDocument>.Filter.Eq("isLegacy", true),
        };

        if (!string.IsNullOrWhiteSpace(customerPhoneNorm))
        {
            filters.Add(Builders<BsonDocument>.Filter.Regex(
                "customerPhone",
                new BsonRegularExpression($"^{Regex.Escape(customerPhoneNorm.Trim())}", "i")));
        }

        return (int)await _returns.CountDocumentsAsync(Builders<BsonDocument>.Filter.And(filters), cancellationToken: ct);
    }

    public async Task<Dictionary<int, decimal>> GetPreviouslyReturnedQtyByLineAsync(
        string storeId,
        string billNo,
        CancellationToken ct = default)
    {
        var returns = await FindAllPostedReturnsForBillAsync(storeId, billNo, ct);
        return AggregateReturnedQtyByLine(returns);
    }

    public static Dictionary<int, decimal> AggregateReturnedQtyByLine(IEnumerable<BsonDocument> returnDocs)
    {
        var map = new Dictionary<int, decimal>();
        foreach (var returnDoc in returnDocs)
        {
            if (!returnDoc.TryGetValue("returnLines", out var linesVal) || !linesVal.IsBsonArray)
            {
                if (!returnDoc.TryGetValue("lines", out linesVal) || !linesVal.IsBsonArray)
                    continue;
            }

            foreach (BsonDocument line in linesVal.AsBsonArray.OfType<BsonDocument>())
            {
                var lineNo = line.Contains("lineNo") ? line["lineNo"].ToInt32() : 0;
                if (lineNo <= 0)
                    continue;

                var returnQty = line.Contains("returnQty")
                    ? (decimal)line["returnQty"].ToDouble()
                    : line.Contains("qty")
                        ? (decimal)line["qty"].ToDouble()
                        : 0m;

                if (returnQty <= 0)
                    continue;

                map.TryGetValue(lineNo, out var existing);
                map[lineNo] = existing + returnQty;
            }
        }

        return map;
    }

    public static bool HasRemainingReturnableQty(BsonDocument billDoc, IReadOnlyDictionary<int, decimal> priorByLine)
    {
        if (!billDoc.Contains("lines") || !billDoc["lines"].IsBsonArray)
            return false;

        foreach (BsonDocument lineBson in billDoc["lines"].AsBsonArray.OfType<BsonDocument>())
        {
            var lineNo = lineBson.GetValue("lineNo", 0).ToInt32();
            var originalQty = (decimal)lineBson.GetValue("qty", 0).ToDouble();
            priorByLine.TryGetValue(lineNo, out var prior);
            if (originalQty - prior > 0)
                return true;
        }

        return false;
    }

    public async Task<bool> HasRemainingReturnableQtyAsync(
        string storeId,
        BsonDocument billDoc,
        CancellationToken ct = default)
    {
        var billNo = billDoc.GetValue("billNo", "").AsString;
        if (string.IsNullOrWhiteSpace(billNo))
            return false;

        var prior = await GetPreviouslyReturnedQtyByLineAsync(storeId, billNo, ct);
        return HasRemainingReturnableQty(billDoc, prior);
    }

    private async Task<List<BsonDocument>> ListCentralReturnsAsync(string originalBillNo, CancellationToken ct)
    {
        using var json = await _storePos!.ListSaleReturnsAsync(originalBillNo, 200, ct);
        var docs = new List<BsonDocument>();
        if (json.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in json.RootElement.EnumerateArray())
                docs.Add(MapCentralReturnToDoc(el));
        }

        return docs;
    }

    private static bool IsPostedNonLegacy(BsonDocument doc) =>
        string.Equals(ReadStatus(doc), "posted", StringComparison.OrdinalIgnoreCase) && !IsLegacyReturn(doc);

    private static string ReadStatus(BsonDocument doc) => ReadString(doc, "status") ?? "posted";

    private static string? ReadString(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var v) || v.IsBsonNull)
            return null;
        return v.IsString ? v.AsString : v.ToString();
    }

    internal static BsonDocument MapCentralReturnToDoc(JsonElement el)
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
}
