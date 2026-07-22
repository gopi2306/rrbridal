using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.App.Services.Billing;

public sealed class SaleReturnHistoryService
{
    private readonly IMongoCollection<BsonDocument> _returns;

    public SaleReturnHistoryService(IMongoDatabase localDb)
    {
        _returns = localDb.GetCollection<BsonDocument>("store_sale_returns");
    }

    public async Task<BsonDocument?> FindFirstPostedReturnForBillAsync(
        string storeId,
        string billNo,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(billNo))
            return null;

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId?.Trim() ?? ""),
            Builders<BsonDocument>.Filter.Eq("originalBillNo", billNo.Trim()),
            Builders<BsonDocument>.Filter.Eq("status", "posted"),
            Builders<BsonDocument>.Filter.Ne("isLegacy", true));

        return await _returns.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<BsonDocument>> FindAllPostedReturnsForBillAsync(
        string storeId,
        string billNo,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(billNo))
            return Array.Empty<BsonDocument>();

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
}
