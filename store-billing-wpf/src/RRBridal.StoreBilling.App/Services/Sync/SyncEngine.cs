using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private readonly IMongoCollection<BsonDocument> _transfers;
    private readonly HttpClient _centralApi;
    private readonly StoreContext _storeContext;
    private readonly MasterDataService _masterData;

    public SyncEngine(IMongoDatabase localDb, HttpClient centralApi, StoreContext storeContext, MasterDataService masterData)
    {
        _outbox = localDb.GetCollection<BsonDocument>("outbox_events");
        _syncState = localDb.GetCollection<BsonDocument>("sync_state");
        _products = localDb.GetCollection<BsonDocument>("local_products_cache");
        _transfers = localDb.GetCollection<BsonDocument>("local_stock_transfers");
        _centralApi = centralApi;
        _storeContext = storeContext;
        _masterData = masterData;
    }

    public async Task<SyncStatus> GetStatusAsync(CancellationToken ct)
    {
        var pending = (int)await _outbox.CountDocumentsAsync(new BsonDocument("status", "pending"), cancellationToken: ct);
        var state = await _syncState.Find(FilterDefinition<BsonDocument>.Empty).FirstOrDefaultAsync(ct);
        var cursor = state?.GetValue("cursor", "0").AsString ?? "0";
        var transferCursor = state?.GetValue("transferCursor", "0").AsString ?? "0";
        var lastError = state?.TryGetValue("lastError", out var e) == true && e.BsonType == BsonType.String ? e.AsString : null;
        if (string.IsNullOrWhiteSpace(lastError)) lastError = null;
        var diagnostics = state?.TryGetValue("diagnosticsSummary", out var d) == true && d.BsonType == BsonType.String
            ? d.AsString
            : null;
        if (string.IsNullOrWhiteSpace(diagnostics)) diagnostics = null;
        return new SyncStatus(pending, cursor, lastError, transferCursor, diagnostics);
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        // Pull before push so intake from this pull creates StockTransferReceived outbox events
        // that are flushed to central in the same cycle (central -> completed in one sync).
        await PullUpdatesAsync(ct);
        await PushPendingAsync(ct);
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
        var transferCursor = state?.GetValue("transferCursor", "0").AsString ?? "0";

        var storeId = _storeContext.StoreId;
        var url =
            $"/api/sync/pull?storeId={Uri.EscapeDataString(storeId)}&sinceCursor={Uri.EscapeDataString(cursor)}&sinceTransferCursor={Uri.EscapeDataString(transferCursor)}&limit=200";

        var res = await _centralApi.GetAsync(url, ct);
        res.EnsureSuccessStatusCode();

        var payload = await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var newCursor = payload.GetProperty("cursor").GetString() ?? cursor;
        var newTransferCursor = transferCursor;
        if (payload.TryGetProperty("transferCursor", out var tcEl) && tcEl.ValueKind == JsonValueKind.String)
        {
            var tcs = tcEl.GetString();
            if (!string.IsNullOrEmpty(tcs)) newTransferCursor = tcs;
        }

        var pullWarnings = new List<string>();
        var pullCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        if (payload.TryGetProperty("updates", out var updatesEl) && updatesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var upd in updatesEl.EnumerateArray())
            {
                var ty = upd.TryGetProperty("type", out var t0) ? t0.GetString() ?? "?" : "?";
                pullCounts.TryGetValue(ty, out var c);
                pullCounts[ty] = c + 1;
            }

            foreach (var upd in updatesEl.EnumerateArray())
            {
                var type = upd.TryGetProperty("type", out var t) ? t.GetString() : null;
                if (type == "ProductUpserted")
                {
                    var product = upd.GetProperty("payload").GetProperty("product");
                    await UpsertProductAsync(product, ct);
                }
                else if (type == "StockTransferAwaitingStoreIntake")
                {
                    var transfer = upd.GetProperty("payload").GetProperty("transfer");
                    await ApplyStockTransferAsync(transfer, ct, createOutbox: true, pullWarnings);
                }
                else if (type == "StockTransferCompleted")
                {
                    var transfer = upd.GetProperty("payload").GetProperty("transfer");
                    await ApplyStockTransferCompletedAsync(transfer, ct, pullWarnings);
                }
            }
        }

        var storeReg = await DescribeCentralStoreRegistrationAsync(storeId, ct);
        var openLocalTransfers = await _transfers.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Ne("stockApplied", true),
            cancellationToken: ct);
        var localProductsInStock = await _products.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Gt("stockQty", 0),
            cancellationToken: ct);

        var countSummary = pullCounts.Count == 0
            ? "updates=0"
            : string.Join(", ", pullCounts.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));
        var diagParts = new List<string>
        {
            $"STORE_ID={storeId}",
            $"pull:{countSummary}",
            $"centralStore={storeReg}",
            $"localTransfersPending={openLocalTransfers}",
            $"localProductsInStock={localProductsInStock}",
        };
        if (pullWarnings.Count > 0)
            diagParts.Add("seeLastError=1");
        var diagnosticsSummary = string.Join(" | ", diagParts);
        if (diagnosticsSummary.Length > 480)
            diagnosticsSummary = diagnosticsSummary[..477] + "...";

        var nextState = new BsonDocument
        {
            { "cursor", newCursor },
            { "transferCursor", newTransferCursor },
            { "updatedAt", DateTime.UtcNow.ToString("O") },
            { "diagnosticsSummary", diagnosticsSummary },
        };

        if (pullWarnings.Count > 0)
            nextState["lastError"] = string.Join("; ", pullWarnings.Distinct());

        await _syncState.ReplaceOneAsync(FilterDefinition<BsonDocument>.Empty, nextState, new ReplaceOptions { IsUpsert = true }, ct);
    }

    /// <summary>
    /// GET /api/stores/:code requires JWT; 404 means STORE_ID will not match central transfers' toStoreId.
    /// </summary>
    private async Task<string> DescribeCentralStoreRegistrationAsync(string storeId, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/stores/{Uri.EscapeDataString(storeId)}");
            using var resp = await _centralApi.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return "unknown_notLoggedIn";
            if (resp.StatusCode == HttpStatusCode.NotFound)
                return "MISSING_fix_STORE_ID";
            if (!resp.IsSuccessStatusCode)
                return $"HTTP_{(int)resp.StatusCode}";
            return "OK";
        }
        catch (Exception ex)
        {
            return "err:" + ex.Message;
        }
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

        var rawSku = doc.TryGetValue("sku", out var sk) && sk.BsonType == BsonType.String ? sk.AsString.Trim() : "";
        var mergedExtraFromDupes = 0.0;
        if (!string.IsNullOrEmpty(rawSku))
            mergedExtraFromDupes = await CollectAndDeleteOtherSkuDocumentsAsync(rawSku, id, ct);

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

        if (mergedExtraFromDupes != 0)
        {
            var baseQty = ReadStockQty(doc);
            doc["stockQty"] = baseQty + mergedExtraFromDupes;
        }

        await _products.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true }, ct);
    }

    /// <summary>
    /// Same-sku rows that are not the canonical central product (e.g. transfer stubs) merged into stock and removed.
    /// </summary>
    private async Task<double> CollectAndDeleteOtherSkuDocumentsAsync(string sku, string centralProductId, CancellationToken ct)
    {
        var sameSku = await _products.Find(Builders<BsonDocument>.Filter.Eq("sku", sku)).ToListAsync(ct);
        if (sameSku.Count <= 1) return 0;

        var extra = 0.0;
        var toDelete = new List<ObjectId>();
        foreach (var row in sameSku)
        {
            var cp = row.GetValue("centralProductId", BsonNull.Value);
            var cpStr = cp.IsBsonNull ? null : cp.AsString;
            if (cpStr == centralProductId) continue;

            extra += ReadStockQty(row);
            if (row.TryGetValue("_id", out var oid) && oid.IsObjectId)
                toDelete.Add(oid.AsObjectId);
        }

        if (toDelete.Count > 0)
        {
            await _products.DeleteManyAsync(
                Builders<BsonDocument>.Filter.In("_id", toDelete),
                cancellationToken: ct);
        }

        return extra;
    }

    private static double ReadStockQty(BsonDocument doc)
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

    private async Task ApplyStockTransferAsync(
        JsonElement transfer,
        CancellationToken ct,
        bool createOutbox,
        List<string> pullWarnings)
    {
        var transferId = transfer.TryGetProperty("transferId", out var idEl) ? idEl.GetString() ?? "" : "";
        var transferNo = transfer.TryGetProperty("transferNo", out var noEl) ? noEl.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(transferId) && string.IsNullOrWhiteSpace(transferNo))
            return;

        var filter = !string.IsNullOrWhiteSpace(transferId)
            ? Builders<BsonDocument>.Filter.Eq("transferId", transferId)
            : Builders<BsonDocument>.Filter.Eq("transferNo", transferNo);

        var existing = await _transfers.Find(filter).FirstOrDefaultAsync(ct);
        if (existing?.TryGetValue("stockApplied", out var appliedVal) == true && appliedVal.IsBoolean && appliedVal.AsBoolean)
            return;

        var lines = ReadTransferLines(transfer);
        if (lines.Count == 0)
        {
            pullWarnings.Add(
                $"StockTransferAwaitingStoreIntake {transferNo.Trim()} ({transferId}): no valid lines (check JSON lines/sku/qty).");
            return;
        }

        Trace.TraceInformation(
            "[Sync] awaiting_intake transferNo={0} transferId={1} lines={2} skus={3}",
            transferNo,
            transferId,
            lines.Count,
            string.Join(",", lines.Select(l => l.Sku)));

        var storeId = _storeContext.StoreId;
        var now = DateTime.UtcNow.ToString("O");
        var stockClassification = ReadTransferStockClassification(transfer);

        if (existing == null)
        {
            var transferDoc = new BsonDocument
            {
                { "transferId", transferId },
                { "transferNo", transferNo },
                { "storeId", storeId },
                { "status", "receiving" },
                { "stockApplied", false },
                { "createdAtUtc", now },
                { "stockClassification", stockClassification },
                { "lines", new BsonArray(lines.Select(l => new BsonDocument
                    {
                        { "sku", l.Sku },
                        { "description", l.Description },
                        { "qty", (double)l.Qty },
                    })) },
            };
            await _transfers.InsertOneAsync(transferDoc, cancellationToken: ct);
        }
        else
        {
            await _transfers.UpdateOneAsync(
                filter,
                Builders<BsonDocument>.Update.Set("stockClassification", stockClassification),
                cancellationToken: ct);
        }

        foreach (var line in lines)
            await IncrementLocalStockForTransferLineAsync(line, now, ct);

        if (createOutbox)
        {
            var eventId = Guid.NewGuid().ToString();
            var payload = new BsonDocument
            {
                { "transferId", transferId },
                { "transferNo", transferNo },
                { "receivedAt", now },
                { "lines", new BsonArray(lines.Select(l => new BsonDocument
                    {
                        { "sku", l.Sku },
                        { "qty", (double)l.Qty },
                    })) },
            };

            var hash = JsonSerializer.Serialize(new
            {
                transferId,
                transferNo,
                lines = lines.Select(l => new { sku = l.Sku, qty = l.Qty }),
            });

            var outboxEvent = new BsonDocument
            {
                { "eventId", eventId },
                { "storeId", storeId },
                { "deviceId", _storeContext.DeviceId },
                { "type", "StockTransferReceived" },
                { "createdAt", now },
                { "payload", payload },
                { "hash", hash },
                { "status", "pending" },
            };
            await _outbox.InsertOneAsync(outboxEvent, cancellationToken: ct);

            var update = Builders<BsonDocument>.Update
                .Set("status", "received")
                .Set("stockApplied", true)
                .Set("receivedAtUtc", now)
                .Set("receiptEventId", eventId)
                .Set("intakeSource", "awaiting_intake_pull")
                .Set("stockClassification", stockClassification);
            await _transfers.UpdateOneAsync(filter, update, cancellationToken: ct);
        }
    }

    /// <summary>
    /// Applies inventory for transfers already completed on central (other POS missed awaiting_intake pull).
    /// Does not enqueue StockTransferReceived (no central push).
    /// </summary>
    private async Task ApplyStockTransferCompletedAsync(JsonElement transfer, CancellationToken ct, List<string> pullWarnings)
    {
        var transferId = transfer.TryGetProperty("transferId", out var idEl) ? idEl.GetString() ?? "" : "";
        var transferNo = transfer.TryGetProperty("transferNo", out var noEl) ? noEl.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(transferId) && string.IsNullOrWhiteSpace(transferNo))
            return;

        var filter = !string.IsNullOrWhiteSpace(transferId)
            ? Builders<BsonDocument>.Filter.Eq("transferId", transferId)
            : Builders<BsonDocument>.Filter.Eq("transferNo", transferNo);

        var existing = await _transfers.Find(filter).FirstOrDefaultAsync(ct);
        if (existing?.TryGetValue("stockApplied", out var appliedVal) == true && appliedVal.IsBoolean && appliedVal.AsBoolean)
            return;

        var lines = ReadTransferLines(transfer);
        if (lines.Count == 0)
        {
            pullWarnings.Add(
                $"StockTransferCompleted {transferNo.Trim()} ({transferId}): no valid lines (check JSON lines/sku/qty).");
            return;
        }

        Trace.TraceInformation(
            "[Sync] completed_transfer transferNo={0} transferId={1} lines={2} skus={3}",
            transferNo,
            transferId,
            lines.Count,
            string.Join(",", lines.Select(l => l.Sku)));

        var storeId = _storeContext.StoreId;
        var now = DateTime.UtcNow.ToString("O");
        var stockClassification = ReadTransferStockClassification(transfer);

        if (existing == null)
        {
            var transferDoc = new BsonDocument
            {
                { "transferId", transferId },
                { "transferNo", transferNo },
                { "storeId", storeId },
                { "status", "received" },
                { "stockApplied", false },
                { "createdAtUtc", now },
                { "stockClassification", stockClassification },
                { "lines", new BsonArray(lines.Select(l => new BsonDocument
                    {
                        { "sku", l.Sku },
                        { "description", l.Description },
                        { "qty", (double)l.Qty },
                    })) },
            };
            await _transfers.InsertOneAsync(transferDoc, cancellationToken: ct);
        }
        else
        {
            await _transfers.UpdateOneAsync(
                filter,
                Builders<BsonDocument>.Update.Set("stockClassification", stockClassification),
                cancellationToken: ct);
        }

        foreach (var line in lines)
            await IncrementLocalStockForTransferLineAsync(line, now, ct);

        var update = Builders<BsonDocument>.Update
            .Set("status", "received")
            .Set("stockApplied", true)
            .Set("receivedAtUtc", now)
            .Set("intakeSource", "central_completed_pull")
            .Set("stockClassification", stockClassification);
        await _transfers.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    private async Task IncrementLocalStockForTransferLineAsync(TransferLine line, string now, CancellationToken ct)
    {
        var sku = line.Sku;
        var escaped = Regex.Escape(sku);
        var skuFilter = Builders<BsonDocument>.Filter.Regex(
            "sku",
            new BsonRegularExpression("^" + escaped + "$", "i"));

        var list = await _products.Find(skuFilter).ToListAsync(ct);
        if (list.Count == 0)
        {
            await _products.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("sku", sku),
                Builders<BsonDocument>.Update
                    .Inc("stockQty", (double)line.Qty)
                    .SetOnInsert("sku", sku)
                    .SetOnInsert("itemName", string.IsNullOrWhiteSpace(line.Description) ? sku : line.Description)
                    .Set("lastStockUpdatedAt", now),
                new UpdateOptions { IsUpsert = true },
                ct);
            return;
        }

        if (list.Count == 1)
        {
            await _products.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", list[0]["_id"]),
                Builders<BsonDocument>.Update
                    .Inc("stockQty", (double)line.Qty)
                    .Set("lastStockUpdatedAt", now),
                cancellationToken: ct);
            return;
        }

        var ordered = list
            .OrderByDescending(d => d.Contains("centralProductId") && !d["centralProductId"].IsBsonNull)
            .ToList();
        var primary = ordered[0];
        var otherStock = 0.0;
        var toDelete = new List<ObjectId>();
        for (var i = 1; i < ordered.Count; i++)
        {
            otherStock += ReadStockQty(ordered[i]);
            if (ordered[i].TryGetValue("_id", out var oid) && oid.IsObjectId)
                toDelete.Add(oid.AsObjectId);
        }

        await _products.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", primary["_id"]),
            Builders<BsonDocument>.Update
                .Inc("stockQty", (double)line.Qty + otherStock)
                .Set("lastStockUpdatedAt", now),
            cancellationToken: ct);

        if (toDelete.Count > 0)
        {
            await _products.DeleteManyAsync(
                Builders<BsonDocument>.Filter.In("_id", toDelete),
                cancellationToken: ct);
        }
    }

    private static bool TryReadLineQty(JsonElement qtyEl, out decimal qty)
    {
        qty = 0;
        switch (qtyEl.ValueKind)
        {
            case JsonValueKind.Number:
                return qtyEl.TryGetDecimal(out qty);
            case JsonValueKind.String:
                return decimal.TryParse(
                    qtyEl.GetString(),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out qty);
            case JsonValueKind.Object:
                if (qtyEl.TryGetProperty("$numberDecimal", out var nd))
                    return TryReadLineQty(nd, out qty);
                if (qtyEl.TryGetProperty("$numberDouble", out var ndb))
                    return TryReadLineQty(ndb, out qty);
                if (qtyEl.TryGetProperty("$numberLong", out var nl))
                    return TryReadLineQty(nl, out qty);
                return false;
            default:
                return false;
        }
    }

    private static string ReadTransferStockClassification(JsonElement transfer, string whenMissing = "Normal Stock")
    {
        if (!transfer.TryGetProperty("stockClassification", out var el) || el.ValueKind == JsonValueKind.Null)
            return whenMissing;
        if (el.ValueKind != JsonValueKind.String)
            return whenMissing;
        var s = el.GetString()?.Trim();
        return string.IsNullOrEmpty(s) ? whenMissing : s;
    }

    private static IReadOnlyList<TransferLine> ReadTransferLines(JsonElement transfer)
    {
        if (!transfer.TryGetProperty("lines", out var linesEl) || linesEl.ValueKind != JsonValueKind.Array)
            return Array.Empty<TransferLine>();

        var lines = new List<TransferLine>();
        foreach (var lineEl in linesEl.EnumerateArray())
        {
            var sku = lineEl.TryGetProperty("sku", out var skuEl) ? skuEl.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(sku))
                continue;

            if (!lineEl.TryGetProperty("qty", out var qtyEl) || !TryReadLineQty(qtyEl, out var qty) || qty <= 0)
                continue;

            var description = lineEl.TryGetProperty("description", out var descEl) ? descEl.GetString() ?? "" : "";
            lines.Add(new TransferLine(sku.Trim(), description.Trim(), qty));
        }

        return lines;
    }
}

internal readonly record struct TransferLine(string Sku, string Description, decimal Qty);

internal static class ObjectExtensions
{
    public static T As<T>(this object? value)
    {
        if (value is null) return default!;
        if (value is T t) return t;
        return (T)Convert.ChangeType(value, typeof(T));
    }
}

