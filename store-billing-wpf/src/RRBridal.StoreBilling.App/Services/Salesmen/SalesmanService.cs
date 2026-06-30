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

namespace RRBridal.StoreBilling.App.Services.Salesmen;

public sealed class SalesmanService
{
    private const string CollectionName = "store_salesmen";

    private readonly IMongoDatabase _localDb;
    private readonly HttpClient _centralApi;
    private readonly StoreContext _storeContext;

    private static readonly JsonSerializerOptions JsonCamel = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public SalesmanService(IMongoDatabase localDb, HttpClient centralApi, StoreContext storeContext)
    {
        _localDb = localDb;
        _centralApi = centralApi;
        _storeContext = storeContext;
    }

    public async Task<IReadOnlyList<SalesmanRecord>> ListAsync(string? search = null, bool activeOnly = false, CancellationToken ct = default)
    {
        var coll = _localDb.GetCollection<BsonDocument>(CollectionName);
        var storeId = _storeContext.StoreId;
        var filter = Builders<BsonDocument>.Filter.Eq("storeId", storeId);
        if (activeOnly)
            filter &= Builders<BsonDocument>.Filter.Eq("isActive", true);

        var docs = await coll.Find(filter).Sort(Builders<BsonDocument>.Sort.Ascending("name")).ToListAsync(ct);
        var rows = docs.Select(MapRecord).ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            rows = rows.Where(r =>
                    r.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.SalesmanCode.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.Phone.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return rows;
    }

    public async Task<SalesmanSaveResult> CreateAsync(string name, string phone, string? salesmanCode = null, CancellationToken ct = default)
    {
        var storeId = _storeContext.StoreId;
        var coll = _localDb.GetCollection<BsonDocument>(CollectionName);
        var code = string.IsNullOrWhiteSpace(salesmanCode)
            ? await new SalesmanCodeGenerator(_localDb).NextAsync(ct)
            : salesmanCode.Trim();

        var localDoc = new BsonDocument
        {
            { "storeId", storeId },
            { "salesmanCode", code },
            { "name", name.Trim() },
            { "phone", phone.Trim() },
            { "isActive", true },
            { "createdAtUtc", DateTime.UtcNow.ToString("O") },
            { "centralId", BsonNull.Value },
            { "centralSyncStatus", "pending" },
            { "lastCentralError", BsonNull.Value },
        };

        await coll.InsertOneAsync(localDoc, cancellationToken: ct);

        string? centralId = null;
        var syncStatus = "pending";
        string? syncWarning = null;

        var body = new CentralCreateSalesmanBody
        {
            StoreId = storeId,
            SalesmanCode = code,
            Name = name.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            IsActive = true,
        };

        try
        {
            using var response = await _centralApi.PostAsJsonAsync("/api/salesmen", body, JsonCamel, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode)
            {
                centralId = TryReadCentralId(raw);
                var centralCode = TryReadSalesmanCode(raw);
                syncStatus = "synced";
                var update = Builders<BsonDocument>.Update
                    .Set("centralId", centralId ?? "")
                    .Set("centralSyncStatus", "synced")
                    .Unset("lastCentralError");
                if (!string.IsNullOrWhiteSpace(centralCode))
                    update = update.Set("salesmanCode", centralCode);
                await coll.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", localDoc["_id"]), update, cancellationToken: ct);
            }
            else
            {
                syncStatus = "failed";
                var err = $"HTTP {(int)response.StatusCode}: {Truncate(raw, 500)}";
                syncWarning = "Saved locally. Central sync failed: " + err;
                await coll.UpdateOneAsync(
                    Builders<BsonDocument>.Filter.Eq("_id", localDoc["_id"]),
                    Builders<BsonDocument>.Update.Set("centralSyncStatus", "failed").Set("lastCentralError", err),
                    cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            syncStatus = "failed";
            syncWarning = "Saved locally. Central sync failed: " + ex.Message;
            await coll.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", localDoc["_id"]),
                Builders<BsonDocument>.Update.Set("centralSyncStatus", "failed").Set("lastCentralError", ex.Message),
                cancellationToken: ct);
        }

        var saved = await coll.Find(Builders<BsonDocument>.Filter.Eq("_id", localDoc["_id"])).FirstOrDefaultAsync(ct);
        return new SalesmanSaveResult
        {
            Record = MapRecord(saved ?? localDoc),
            CentralSyncWarning = syncWarning,
            CentralSyncStatus = syncStatus,
        };
    }

    public async Task<SalesmanSaveResult> UpdateAsync(SalesmanRecord selected, string name, string phone, bool isActive, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(selected.LocalMongoId))
            throw new InvalidOperationException("Salesman local id is missing.");

        var coll = _localDb.GetCollection<BsonDocument>(CollectionName);
        var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(selected.LocalMongoId));

        await coll.UpdateOneAsync(
            filter,
            Builders<BsonDocument>.Update
                .Set("name", name.Trim())
                .Set("phone", phone.Trim())
                .Set("isActive", isActive),
            cancellationToken: ct);

        string? syncWarning = null;
        var syncStatus = selected.CentralSyncStatus;

        if (!string.IsNullOrWhiteSpace(selected.CentralId))
        {
            var body = new CentralUpdateSalesmanBody
            {
                Name = name.Trim(),
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
                IsActive = isActive,
            };

            try
            {
                using var response = await _centralApi.PatchAsJsonAsync($"/api/salesmen/{selected.CentralId}", body, JsonCamel, ct);
                var raw = await response.Content.ReadAsStringAsync(ct);
                if (response.IsSuccessStatusCode)
                {
                    syncStatus = "synced";
                    await coll.UpdateOneAsync(
                        filter,
                        Builders<BsonDocument>.Update.Set("centralSyncStatus", "synced").Unset("lastCentralError"),
                        cancellationToken: ct);
                }
                else
                {
                    syncStatus = "failed";
                    syncWarning = $"Central update failed: HTTP {(int)response.StatusCode}: {Truncate(raw, 300)}";
                    await coll.UpdateOneAsync(
                        filter,
                        Builders<BsonDocument>.Update.Set("centralSyncStatus", "failed").Set("lastCentralError", syncWarning),
                        cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                syncStatus = "failed";
                syncWarning = "Central update failed: " + ex.Message;
                await coll.UpdateOneAsync(
                    filter,
                    Builders<BsonDocument>.Update.Set("centralSyncStatus", "failed").Set("lastCentralError", ex.Message),
                    cancellationToken: ct);
            }
        }

        var saved = await coll.Find(filter).FirstOrDefaultAsync(ct);
        return new SalesmanSaveResult
        {
            Record = MapRecord(saved!),
            CentralSyncWarning = syncWarning,
            CentralSyncStatus = syncStatus,
        };
    }

    public async Task UpsertFromCentralJsonAsync(JsonElement el, CancellationToken ct = default)
    {
        var id = ReadJsonId(el);
        if (string.IsNullOrWhiteSpace(id)) return;

        var coll = _localDb.GetCollection<BsonDocument>(CollectionName);
        var filter = Builders<BsonDocument>.Filter.Eq("centralId", id);
        var existing = await coll.Find(filter).FirstOrDefaultAsync(ct);

        var doc = new BsonDocument
        {
            { "centralId", id },
            { "storeId", el.TryGetProperty("storeId", out var s) ? s.GetString() ?? "" : "" },
            { "salesmanCode", el.TryGetProperty("salesmanCode", out var c) ? c.GetString() ?? "" : "" },
            { "name", el.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "" },
            { "phone", el.TryGetProperty("phone", out var p) ? p.GetString() ?? "" : "" },
            { "isActive", el.TryGetProperty("isActive", out var a) && a.ValueKind == JsonValueKind.False ? false : true },
            { "centralSyncStatus", "synced" },
            { "lastSyncedAt", DateTime.UtcNow.ToString("O") },
        };

        if (existing != null)
            doc["_id"] = existing["_id"];
        else if (el.TryGetProperty("salesmanCode", out var codeEl))
        {
            var code = codeEl.GetString() ?? "";
            var byCode = await coll.Find(
                Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("storeId", _storeContext.StoreId),
                    Builders<BsonDocument>.Filter.Eq("salesmanCode", code)))
                .FirstOrDefaultAsync(ct);
            if (byCode != null)
                doc["_id"] = byCode["_id"];
        }

        await coll.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true }, ct);
    }

    private static SalesmanRecord MapRecord(BsonDocument doc) => new()
    {
        LocalMongoId = doc.Contains("_id") ? doc["_id"].ToString()! : "",
        CentralId = doc.GetValue("centralId", "").IsBsonNull ? "" : doc["centralId"].AsString,
        SalesmanCode = doc.GetValue("salesmanCode", "").AsString,
        Name = doc.GetValue("name", "").AsString,
        Phone = doc.GetValue("phone", "").AsString,
        IsActive = !doc.Contains("isActive") || doc["isActive"].AsBoolean,
        CentralSyncStatus = doc.GetValue("centralSyncStatus", "synced").AsString,
    };

    private static string ReadJsonId(JsonElement el)
    {
        if (!el.TryGetProperty("_id", out var idEl)) return "";
        if (idEl.ValueKind == JsonValueKind.String) return idEl.GetString() ?? "";
        if (idEl.ValueKind == JsonValueKind.Object && idEl.TryGetProperty("$oid", out var oid))
            return oid.GetString() ?? "";
        return idEl.GetRawText().Trim('"');
    }

    private static string? TryReadSalesmanCode(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("salesmanCode", out var codeEl) && codeEl.ValueKind == JsonValueKind.String)
                return codeEl.GetString();
        }
        catch { /* ignore */ }

        return null;
    }

    private static string? TryReadCentralId(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("_id", out var idEl))
            {
                if (idEl.ValueKind == JsonValueKind.String) return idEl.GetString();
                if (idEl.ValueKind == JsonValueKind.Object && idEl.TryGetProperty("$oid", out var oid))
                    return oid.GetString();
            }
        }
        catch { /* ignore */ }

        return null;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s[..max] + "…";
    }

    private sealed class CentralCreateSalesmanBody
    {
        public required string StoreId { get; init; }
        public string? SalesmanCode { get; init; }
        public required string Name { get; init; }
        public string? Phone { get; init; }
        public bool IsActive { get; init; }
    }

    private sealed class CentralUpdateSalesmanBody
    {
        public string? Name { get; init; }
        public string? Phone { get; init; }
        public bool? IsActive { get; init; }
    }
}

public sealed class SalesmanSaveResult
{
    public required SalesmanRecord Record { get; init; }
    public string? CentralSyncWarning { get; init; }
    public string CentralSyncStatus { get; init; } = "synced";
}
