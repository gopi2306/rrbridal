using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.App.Services.Products;

/// <summary>Resolves display HSN from product cache + local master_hsn_codes (never raw ObjectIds).</summary>
public static class HsnSacResolver
{
    private static readonly Regex ObjectIdLike = new(@"^[a-fA-F0-9]{24}$", RegexOptions.Compiled);

    public static bool LooksLikeObjectId(string? value) =>
        !string.IsNullOrWhiteSpace(value) && ObjectIdLike.IsMatch(value.Trim());

    public static async Task<Dictionary<string, string>> LoadLookupAsync(IMongoDatabase localDb, CancellationToken ct = default)
    {
        var collection = localDb.GetCollection<BsonDocument>("master_hsn_codes");
        var docs = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct);
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in docs)
        {
            if (!d.TryGetValue("centralId", out var cid) || cid.IsBsonNull) continue;
            if (!d.TryGetValue("hsnCode", out var hc) || hc.IsBsonNull) continue;
            var code = hc.IsString ? hc.AsString.Trim() : hc.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(code)) continue;
            lookup[cid.ToString()!] = code;
        }

        return lookup;
    }

    public static string Resolve(BsonDocument product, IReadOnlyDictionary<string, string>? hsnByCentralId)
    {
        var hsnSac = ReadString(product, "hsnSac");
        if (!string.IsNullOrWhiteSpace(hsnSac) && !LooksLikeObjectId(hsnSac))
            return hsnSac.Trim();

        var hsnCode = ReadString(product, "hsnCode");
        if (!string.IsNullOrWhiteSpace(hsnCode) && !LooksLikeObjectId(hsnCode))
            return hsnCode.Trim();

        var hsnCodeId = ReadString(product, "hsnCodeId");
        if (!string.IsNullOrWhiteSpace(hsnCodeId) && hsnByCentralId != null)
        {
            var id = hsnCodeId.Trim();
            if (hsnByCentralId.TryGetValue(id, out var fromMaster) && !string.IsNullOrWhiteSpace(fromMaster))
                return fromMaster.Trim();
        }

        if (!string.IsNullOrWhiteSpace(hsnSac) && LooksLikeObjectId(hsnSac) && hsnByCentralId != null
            && hsnByCentralId.TryGetValue(hsnSac.Trim(), out var fromMisplaced))
            return fromMisplaced.Trim();

        return "";
    }

    private static string? ReadString(BsonDocument d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v.IsBsonNull)
            return null;
        return v.IsString ? v.AsString : v.ToString();
    }
}
