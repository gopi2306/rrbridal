using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class StoreAnalyticsService
{
    private readonly IMongoDatabase _db;

    public StoreAnalyticsService(IMongoDatabase localDb)
    {
        _db = localDb;
    }

    /// <summary>
    /// Builds a day-by-day sales view from posted <c>store_bills</c> for the given store (UTC days).
    /// </summary>
    public async Task<StoreAnalyticsSnapshot> LoadAsync(string storeId, int dayCount = 14, CancellationToken ct = default)
    {
        dayCount = Math.Clamp(dayCount, 1, 90);
        var billsColl = _db.GetCollection<BsonDocument>("store_bills");
        var storeFilter = Builders<BsonDocument>.Filter.Eq("storeId", storeId);
        var billDocs = await billsColl.Find(storeFilter).ToListAsync(ct);

        var todayUtc = DateTime.UtcNow.Date;
        var startUtc = todayUtc.AddDays(-(dayCount - 1));

        var byDay = new Dictionary<DateTime, (int Count, decimal Revenue)>();
        foreach (var doc in billDocs)
        {
            if (!TryGetUtcDate(doc, "createdAtUtc", out var created))
                continue;
            var day = created.Date;
            if (day < startUtc || day > todayUtc)
                continue;
            var payable = ReadDecimal(doc, "payable");
            if (!byDay.TryGetValue(day, out var cur))
                cur = (0, 0m);
            byDay[day] = (cur.Count + 1, cur.Revenue + payable);
        }

        var rows = new List<DailySalesRow>(dayCount);
        for (var d = startUtc; d <= todayUtc; d = d.AddDays(1))
        {
            byDay.TryGetValue(d, out var agg);
            rows.Add(new DailySalesRow
            {
                DayUtc = d,
                DayLabel = d.ToString("ddd, dd-MMM-yyyy", CultureInfo.InvariantCulture) + " UTC",
                BillsCount = agg.Count,
                Revenue = agg.Revenue,
            });
        }

        rows.Reverse();

        var totalBills = rows.Sum(r => r.BillsCount);
        var totalRev = rows.Sum(r => r.Revenue);

        return new StoreAnalyticsSnapshot
        {
            PeriodLabel = $"{dayCount} days ending {todayUtc:yyyy-MM-dd} (UTC)",
            TotalBillsInPeriod = totalBills,
            TotalRevenueInPeriod = totalRev,
            DailyRows = rows,
        };
    }

    private static bool TryGetUtcDate(BsonDocument doc, string key, out DateTime utc)
    {
        utc = default;
        if (!doc.TryGetValue(key, out var v) || v.IsBsonNull)
            return false;
        if (v.IsString)
        {
            if (!DateTime.TryParse(v.AsString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                return false;
            utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
            return true;
        }

        if (v.IsBsonDateTime)
        {
            utc = DateTime.SpecifyKind(v.ToUniversalTime(), DateTimeKind.Utc);
            return true;
        }

        return false;
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
