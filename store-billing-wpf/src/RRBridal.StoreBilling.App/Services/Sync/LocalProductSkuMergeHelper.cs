using System.Collections.Generic;
using MongoDB.Bson;

namespace RRBridal.StoreBilling.App.Services.Sync;

/// <summary>
/// Merges transfer/adjustment stub product rows into the canonical central-product cache row.
/// </summary>
public static class LocalProductSkuMergeHelper
{
    public sealed record MergeResult(double ExtraStock, IReadOnlyList<ObjectId> IdsToDelete);

    /// <summary>
    /// Selects same-SKU rows that are not the canonical central product (missing or different centralProductId).
    /// Their stock is merged into the upsert; those documents are deleted.
    /// </summary>
    public static MergeResult CollectOtherSkuDocuments(
        IReadOnlyList<BsonDocument> sameSkuRows,
        string centralProductId)
    {
        var extra = 0.0;
        var toDelete = new List<ObjectId>();
        foreach (var row in sameSkuRows)
        {
            var cp = row.GetValue("centralProductId", BsonNull.Value);
            var cpStr = cp.IsBsonNull ? null : cp.AsString;
            if (cpStr == centralProductId)
                continue;

            extra += ReadStockQty(row);
            if (row.TryGetValue("_id", out var oid) && oid.IsObjectId)
                toDelete.Add(oid.AsObjectId);
        }

        return new MergeResult(extra, toDelete);
    }

    public static double ReadStockQty(BsonDocument doc)
    {
        if (!doc.TryGetValue("stockQty", out var v) || v.IsBsonNull) return 0;
        return v.BsonType switch
        {
            BsonType.Double => v.AsDouble,
            BsonType.Int32 => v.AsInt32,
            BsonType.Int64 => v.AsInt64,
            BsonType.Decimal128 => (double)v.AsDecimal,
            _ => 0,
        };
    }
}
