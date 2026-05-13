using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class StoreDashboardService
{
    private readonly IMongoDatabase _db;

    public StoreDashboardService(IMongoDatabase localDb)
    {
        _db = localDb;
    }

    public async Task<StoreDashboardSnapshot> LoadAsync(string storeId, CancellationToken ct = default)
    {
        var billsColl = _db.GetCollection<BsonDocument>("store_bills");
        var outboxColl = _db.GetCollection<BsonDocument>("outbox_events");
        var syncColl = _db.GetCollection<BsonDocument>("sync_state");
        var productsColl = _db.GetCollection<BsonDocument>("local_products_cache");

        var storeFilter = Builders<BsonDocument>.Filter.Eq("storeId", storeId);
        var billDocs = await billsColl.Find(storeFilter).ToListAsync(ct);

        var todayUtc = DateTime.UtcNow.Date;
        var weekStart = todayUtc.AddDays(-7);

        var billsToday = 0;
        var revenueToday = 0m;
        var billsWeek = 0;
        var revenueWeek = 0m;

        foreach (var doc in billDocs)
        {
            if (!TryGetUtcDate(doc, "createdAtUtc", out var created))
                continue;

            var day = created.Date;
            var payable = ReadDecimal(doc, "payable");

            if (day == todayUtc)
            {
                billsToday++;
                revenueToday += payable;
            }

            if (day >= weekStart)
            {
                billsWeek++;
                revenueWeek += payable;
            }
        }

        var recent = billDocs
            .Select(d =>
            {
                DateTime? dt = null;
                if (TryGetUtcDate(d, "createdAtUtc", out var parsed))
                    dt = parsed;
                return (Doc: d, Dt: dt);
            })
            .Where(x => x.Dt.HasValue)
            .OrderByDescending(x => x.Dt)
            .Take(10)
            .Select(x =>
            {
                var payable = ReadDecimal(x.Doc, "payable");
                var billNo = x.Doc.TryGetValue("billNo", out var bn) && bn.IsString ? bn.AsString : "";
                return new DashboardRecentBill
                {
                    BillNo = billNo,
                    CreatedAtDisplay = x.Dt!.Value.ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC",
                    Payable = payable,
                };
            })
            .ToList();

        var pendingOutbox = (int)await outboxColl.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Eq("status", "pending"),
            cancellationToken: ct);

        var syncDoc = await syncColl.Find(FilterDefinition<BsonDocument>.Empty).FirstOrDefaultAsync(ct);
        var cursor = "0";
        string? syncAt = null;
        if (syncDoc != null)
        {
            if (syncDoc.TryGetValue("cursor", out var c) && c.IsString)
                cursor = c.AsString;
            if (syncDoc.TryGetValue("updatedAt", out var u))
                syncAt = u.IsString ? u.AsString : u.ToString();
        }

        long productCount;
        try
        {
            var activeFilter = Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Eq("isActive", true),
                Builders<BsonDocument>.Filter.Not(Builders<BsonDocument>.Filter.Exists("isActive")));
            productCount = await productsColl.CountDocumentsAsync(activeFilter, cancellationToken: ct);
        }
        catch
        {
            productCount = await productsColl.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: ct);
        }

        return new StoreDashboardSnapshot
        {
            StoreId = storeId,
            BillsTodayCount = billsToday,
            BillsTodayRevenue = revenueToday,
            BillsLast7DaysCount = billsWeek,
            BillsLast7DaysRevenue = revenueWeek,
            PendingOutboxCount = pendingOutbox,
            SyncCursor = cursor,
            SyncUpdatedAt = syncAt,
            ProductCacheCount = productCount,
            RecentBills = recent,
        };
    }

    private static bool TryGetUtcDate(BsonDocument doc, string key, out DateTime utc)
    {
        utc = default;
        if (!doc.TryGetValue(key, out var v) || v.IsBsonNull || !v.IsString)
            return false;
        if (!DateTime.TryParse(v.AsString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return false;
        utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        return true;
    }

    private static decimal ReadDecimal(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var v) || v.IsBsonNull)
            return 0m;
        return v switch
        {
            { IsDouble: true } => (decimal)v.AsDouble,
            { IsInt32: true } => v.AsInt32,
            { IsInt64: true } => v.AsInt64,
            { IsDecimal128: true } => (decimal)v.AsDecimal128,
            { IsString: true } => decimal.TryParse(v.AsString, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m,
            _ => 0m,
        };
    }
}
