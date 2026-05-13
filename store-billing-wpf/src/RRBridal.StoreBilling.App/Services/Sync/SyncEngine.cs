using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Masters;

namespace RRBridal.StoreBilling.App.Services.Sync;

public sealed class SyncEngine : ISyncEngine
{
    private readonly IMongoCollection<BsonDocument> _outbox;
    private readonly IMongoCollection<BsonDocument> _syncState;
    private readonly IMongoCollection<BsonDocument> _products;
    private readonly HttpClient _centralApi;
    private readonly StoreContext _storeContext;
    private readonly MasterDataService _masterData;

    public SyncEngine(IMongoDatabase localDb, HttpClient centralApi, StoreContext storeContext, MasterDataService masterData)
    {
        _outbox = localDb.GetCollection<BsonDocument>("outbox_events");
        _syncState = localDb.GetCollection<BsonDocument>("sync_state");
        _products = localDb.GetCollection<BsonDocument>("local_products_cache");
        _centralApi = centralApi;
        _storeContext = storeContext;
        _masterData = masterData;
    }

    public async Task<SyncStatus> GetStatusAsync(CancellationToken ct)
    {
        var pending = (int)await _outbox.CountDocumentsAsync(new BsonDocument("status", "pending"), cancellationToken: ct);
        var state = await _syncState.Find(FilterDefinition<BsonDocument>.Empty).FirstOrDefaultAsync(ct);
        var cursor = state?.GetValue("cursor", "0").AsString ?? "0";
        var lastError = state?.TryGetValue("lastError", out var e) == true ? e.AsString : null;
        return new SyncStatus(pending, cursor, lastError);
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        await PushPendingAsync(ct);
        await PullUpdatesAsync(ct);
        try { await _masterData.SyncAllMastersAsync(ct); } catch { /* master sync is best-effort */ }
        try { await SyncStoreUsersAsync(ct); } catch { /* store user sync is best-effort */ }
    }

    private async Task PushPendingAsync(CancellationToken ct)
    {
        var pending = await _outbox
            .Find(new BsonDocument("status", "pending"))
            .Sort(new BsonDocument("createdAt", 1))
            .Limit(200)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        var events = pending.Select(d => new
        {
            eventId = d["eventId"].AsString,
            storeId = d["storeId"].AsString,
            deviceId = d["deviceId"].AsString,
            type = d["type"].AsString,
            createdAt = d["createdAt"].AsString,
            payload = BsonTypeMapper.MapToDotNetValue(d["payload"]).As<object>(),
            hash = d.GetValue("hash", "").AsString,
        });

        var res = await _centralApi.PostAsJsonAsync("/api/sync/push", new { events }, ct);
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var results = doc.RootElement.GetProperty("results").EnumerateArray().ToDictionary(
            x => x.GetProperty("eventId").GetString() ?? "",
            x => x.GetProperty("status").GetString() ?? "");

        foreach (var ev in pending)
        {
            var id = ev["eventId"].AsString;
            if (!results.TryGetValue(id, out var status)) continue;
            if (status is "applied" or "duplicate")
            {
                var filter = Builders<BsonDocument>.Filter.Eq("eventId", id);
                var update = Builders<BsonDocument>.Update.Set("status", "synced").Set("syncedAt", DateTime.UtcNow.ToString("O"));
                await _outbox.UpdateOneAsync(filter, update, cancellationToken: ct);
            }
        }
    }

    private async Task PullUpdatesAsync(CancellationToken ct)
    {
        var state = await _syncState.Find(FilterDefinition<BsonDocument>.Empty).FirstOrDefaultAsync(ct);
        var cursor = state?.GetValue("cursor", "0").AsString ?? "0";

        var storeId = _storeContext.StoreId;
        var url = $"/api/sync/pull?storeId={Uri.EscapeDataString(storeId)}&sinceCursor={Uri.EscapeDataString(cursor)}&limit=200";

        var res = await _centralApi.GetAsync(url, ct);
        res.EnsureSuccessStatusCode();

        var payload = await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var newCursor = payload.GetProperty("cursor").GetString() ?? cursor;

        if (payload.TryGetProperty("updates", out var updatesEl) && updatesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var upd in updatesEl.EnumerateArray())
            {
                var type = upd.TryGetProperty("type", out var t) ? t.GetString() : null;
                if (type == "ProductUpserted")
                {
                    var product = upd.GetProperty("payload").GetProperty("product");
                    await UpsertProductAsync(product, ct);
                }
            }
        }

        var nextState = new BsonDocument
        {
            { "cursor", newCursor },
            { "updatedAt", DateTime.UtcNow.ToString("O") },
        };

        await _syncState.ReplaceOneAsync(FilterDefinition<BsonDocument>.Empty, nextState, new ReplaceOptions { IsUpsert = true }, ct);
    }

    public async Task SyncStoreUsersAsync(CancellationToken ct)
    {
        var storeId = _storeContext.StoreId;
        var url = $"/api/store-users?storeId={Uri.EscapeDataString(storeId)}";
        var res = await _centralApi.GetAsync(url, ct);
        if (!res.IsSuccessStatusCode) return;

        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return;

        var collection = _outbox.Database.GetCollection<BsonDocument>("store_users");

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var id = el.TryGetProperty("_id", out var idEl)
                ? idEl.ValueKind == JsonValueKind.String ? idEl.GetString() ?? "" : idEl.GetRawText()
                : "";
            if (string.IsNullOrWhiteSpace(id)) continue;

            var bsonDoc = new BsonDocument
            {
                { "centralId", id },
                { "email", el.TryGetProperty("email", out var e) ? e.GetString() ?? "" : "" },
                { "name", el.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "" },
                { "role", el.TryGetProperty("role", out var r) ? r.GetString() ?? "" : "" },
                { "passwordHash", el.TryGetProperty("passwordHash", out var ph) ? ph.GetString() ?? "" : "" },
                { "storeId", el.TryGetProperty("storeId", out var s) ? s.GetString() ?? "" : "" },
                { "locationKind", el.TryGetProperty("locationKind", out var lk) ? lk.GetString() ?? "" : "" },
                { "lastSyncedAt", DateTime.UtcNow.ToString("O") },
            };

            var filter = Builders<BsonDocument>.Filter.Eq("centralId", id);
            await collection.ReplaceOneAsync(filter, bsonDoc, new ReplaceOptions { IsUpsert = true }, ct);
        }
    }

    private async Task UpsertProductAsync(JsonElement product, CancellationToken ct)
    {
        var id = product.GetProperty("_id").GetString();
        if (string.IsNullOrWhiteSpace(id)) return;

        var doc = BsonDocument.Parse(product.GetRawText());
        doc["centralProductId"] = id;
        doc["lastSyncedAt"] = DateTime.UtcNow.ToString("O");

        var filter = Builders<BsonDocument>.Filter.Eq("centralProductId", id);

        var existing = await _products.Find(filter).FirstOrDefaultAsync(ct);
        if (existing != null && existing.TryGetValue("stockQty", out var sqVal) && !sqVal.IsBsonNull)
        {
            doc["stockQty"] = sqVal;
        }
        else if (!doc.Contains("stockQty"))
        {
            doc["stockQty"] = 0;
        }

        await _products.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true }, ct);
    }
}

internal static class ObjectExtensions
{
    public static T As<T>(this object? value)
    {
        if (value is null) return default!;
        if (value is T t) return t;
        return (T)Convert.ChangeType(value, typeof(T));
    }
}

