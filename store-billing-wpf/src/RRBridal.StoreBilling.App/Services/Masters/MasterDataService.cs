using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.App.Services.Masters;

public sealed class MasterDataService
{
    private static readonly string[] MasterTypes =
    [
        "manufacturers",
        "departments",
        "categories",
        "sub-categories",
        "brands",
        "weight-sizes",
        "weight-units",
        "offer-groups",
        "product-statuses",
        "colours",
        "hsn-codes",
        "gst-uoms",
        "uom-subs",
        "batch-expiry-details",
        "item-prep-statuses",
        "packed-confirmations",
        "po-qty-policies",
        "sell-by-types",
        "batch-selections",
        "sku-types",
        "sku-order-groups",
        "indent-types",
    ];

    private readonly IMongoDatabase _localDb;
    private readonly HttpClient _http;

    public MasterDataService(IMongoDatabase localDb, HttpClient centralApi)
    {
        _localDb = localDb;
        _http = centralApi;
    }

    public async Task SyncAllMastersAsync(CancellationToken ct = default)
    {
        foreach (var masterType in MasterTypes)
        {
            try
            {
                await SyncMasterAsync(masterType, ct);
            }
            catch
            {
                // Individual master sync failure should not block others
            }
        }
    }

    public async Task<IReadOnlyList<MasterItem>> GetAsync(string masterType, CancellationToken ct = default)
    {
        var collectionName = $"master_{masterType.Replace("-", "_")}";
        var collection = _localDb.GetCollection<BsonDocument>(collectionName);
        var docs = await collection.Find(FilterDefinition<BsonDocument>.Empty).Sort(new BsonDocument("name", 1)).ToListAsync(ct);

        return docs.Select(d => new MasterItem
        {
            Id = d.TryGetValue("centralId", out var cid) && !cid.IsBsonNull ? cid.AsString : d["_id"].ToString()!,
            Code = d.TryGetValue("code", out var c) && !c.IsBsonNull ? c.AsString : "",
            Name = d.TryGetValue("name", out var n) && !n.IsBsonNull ? n.AsString : "",
            IsActive = !d.TryGetValue("isActive", out var a) || a.IsBsonNull || a.AsBoolean,
        }).ToList();
    }

    private async Task SyncMasterAsync(string masterType, CancellationToken ct)
    {
        var res = await _http.GetAsync($"/api/{masterType}", ct);
        if (!res.IsSuccessStatusCode) return;

        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return;

        var collectionName = $"master_{masterType.Replace("-", "_")}";
        var collection = _localDb.GetCollection<BsonDocument>(collectionName);

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var id = el.TryGetProperty("_id", out var idEl)
                ? idEl.ValueKind == JsonValueKind.String ? idEl.GetString() ?? "" : idEl.GetRawText()
                : "";
            if (string.IsNullOrWhiteSpace(id)) continue;

            var bsonDoc = new BsonDocument
            {
                { "centralId", id },
                { "code", el.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "" },
                { "name", el.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "" },
                { "isActive", !el.TryGetProperty("isActive", out var a) || a.GetBoolean() },
                { "lastSyncedAt", DateTime.UtcNow.ToString("O") },
            };

            if (masterType == "hsn-codes")
            {
                if (el.TryGetProperty("hsnCode", out var hsn) && hsn.ValueKind == JsonValueKind.String)
                    bsonDoc["hsnCode"] = hsn.GetString() ?? "";
                if (el.TryGetProperty("gstPercent", out var gst) && gst.ValueKind == JsonValueKind.Number)
                    bsonDoc["gstPercent"] = gst.GetDouble();
            }

            var filter = Builders<BsonDocument>.Filter.Eq("centralId", id);
            await collection.ReplaceOneAsync(filter, bsonDoc, new ReplaceOptions { IsUpsert = true }, ct);
        }
    }
}
