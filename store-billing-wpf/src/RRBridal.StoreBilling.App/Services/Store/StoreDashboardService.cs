using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Api;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class StoreDashboardService
{
    private static readonly JsonElement EmptyJson = JsonDocument.Parse("{}").RootElement;

    private readonly IMongoDatabase _db;
    private CentralOnlineModeService? _centralMode;
    private CentralDashboardClient? _dashboardApi;

    public StoreDashboardService(IMongoDatabase localDb)
    {
        _db = localDb;
    }

    public void ConfigureOnline(CentralOnlineModeService centralMode, CentralDashboardClient dashboardApi)
    {
        _centralMode = centralMode;
        _dashboardApi = dashboardApi;
    }

    public bool IsCentralOnline => _centralMode?.IsOnlineMode == true && _dashboardApi != null;

    public async Task<IReadOnlyList<string>> GetDistinctPosCountersAsync(string storeId, CancellationToken ct = default)
    {
        var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i <= 3; i++)
            merged.Add(i.ToString(CultureInfo.InvariantCulture));

        if (IsCentralOnline)
        {
            var businessDate = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            using var dayCloseJson = await _dashboardApi!.GetStoreDayCloseAsync(businessDate, null, ct);
            foreach (var counter in StoreDayCloseDashboardReader.ReadCounters(dayCloseJson.RootElement))
            {
                if (!string.IsNullOrWhiteSpace(counter.PosCounter))
                    merged.Add(counter.PosCounter.Trim());
            }

            return merged
                .OrderBy(p => int.TryParse(p, out var n) ? n : int.MaxValue)
                .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var billsColl = _db.GetCollection<BsonDocument>("store_bills");
        var storeFilter = Builders<BsonDocument>.Filter.Eq("storeId", storeId);
        var billDocs = await billsColl.Find(storeFilter).ToListAsync(ct);

        var fromDb = billDocs
            .Select(ReadPosCounter)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var p in fromDb)
            merged.Add(p!);

        return merged
            .OrderBy(p => int.TryParse(p, out var n) ? n : int.MaxValue)
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task<StoreDashboardSnapshot> LoadAsync(
        string storeId,
        ReportScope scope = ReportScope.ThisCounter,
        string? deviceId = null,
        string? posCounterFilter = null,
        CancellationToken ct = default)
    {
        return IsCentralOnline
            ? LoadOnlineAsync(storeId, posCounterFilter, ct)
            : LoadOfflineAsync(storeId, scope, deviceId, posCounterFilter, ct);
    }

    /// <summary>Central dashboard is fail-closed: any API failure propagates so the caller shows an error.</summary>
    private async Task<StoreDashboardSnapshot> LoadOnlineAsync(
        string storeId,
        string? posCounterFilter,
        CancellationToken ct)
    {
        using var todayDoc = await _dashboardApi!.GetStoreSalesAsync("today", billLimit: 10, ct: ct);
        using var weekDoc = await _dashboardApi.GetStoreSalesAsync("week", ct: ct);
        using var opsDoc = await _dashboardApi.GetStoreOpsAsync(ct);

        var todayRoot = todayDoc.RootElement;
        var weekRoot = weekDoc.RootElement;
        var opsRoot = opsDoc.RootElement;

        var todaySummary = todayRoot.TryGetProperty("summary", out var ts) ? ts : EmptyJson;
        var weekSummary = weekRoot.TryGetProperty("summary", out var ws) ? ws : EmptyJson;

        var billsTodayCount = CentralDashboardClient.ReadInt(todaySummary, "invoices");
        var billsTodayRevenue = CentralDashboardClient.ReadDecimal(
            todaySummary, "netSales", CentralDashboardClient.ReadDecimal(todaySummary, "totalBillAmount"));

        var billsWeekCount = CentralDashboardClient.ReadInt(weekSummary, "invoices");
        var billsWeekRevenue = CentralDashboardClient.ReadDecimal(
            weekSummary, "netSales", CentralDashboardClient.ReadDecimal(weekSummary, "totalBillAmount"));

        var productCacheCount = 0L;
        var totalAvailableQty = 0m;
        if (opsRoot.TryGetProperty("metrics", out var metrics))
        {
            productCacheCount = CentralDashboardClient.ReadLong(metrics, "totalSkus");
            totalAvailableQty = CentralDashboardClient.ReadDecimal(metrics, "onShelfUnits");
        }

        var recentBills = new List<DashboardRecentBill>();
        if (todayRoot.TryGetProperty("bills", out var billsEl)
            && billsEl.TryGetProperty("data", out var billsData)
            && billsData.ValueKind == JsonValueKind.Array)
        {
            foreach (var billEl in billsData.EnumerateArray())
            {
                var pos = CentralDashboardClient.ReadString(billEl, "posCounter");
                if (!string.IsNullOrWhiteSpace(posCounterFilter)
                    && !string.Equals(pos, posCounterFilter.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;

                var occurredAtRaw = CentralDashboardClient.ReadString(billEl, "occurredAt");
                recentBills.Add(new DashboardRecentBill
                {
                    BillNo = CentralDashboardClient.ReadString(billEl, "billNo"),
                    CreatedAtDisplay = FormatOccurredAtLocal(occurredAtRaw),
                    Payable = CentralDashboardClient.ReadDecimal(billEl, "payable"),
                    CounterDisplay = CounterDisplayFormatter.Format(pos, null),
                });

                if (recentBills.Count >= 10)
                    break;
            }
        }

        return new StoreDashboardSnapshot
        {
            StoreId = storeId,
            Scope = ReportScope.StoreWide,
            BillsTodayCount = billsTodayCount,
            BillsTodayRevenue = billsTodayRevenue,
            BillsLast7DaysCount = billsWeekCount,
            BillsLast7DaysRevenue = billsWeekRevenue,
            StoreWideBillsTodayCount = null,
            StoreWideBillsTodayRevenue = null,
            ProductCacheCount = productCacheCount,
            TotalAvailableQty = totalAvailableQty,
            RecentBills = recentBills,
        };
    }

    private static string FormatOccurredAtLocal(string occurredAtRaw)
    {
        if (string.IsNullOrWhiteSpace(occurredAtRaw))
            return "";
        if (!DateTime.TryParse(occurredAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return occurredAtRaw;
        var local = dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
        return local.ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture);
    }

    private async Task<StoreDashboardSnapshot> LoadOfflineAsync(
        string storeId,
        ReportScope scope,
        string? deviceId,
        string? posCounterFilter,
        CancellationToken ct)
    {
        var billsColl = _db.GetCollection<BsonDocument>("store_bills");
        var productsColl = _db.GetCollection<BsonDocument>("local_products_cache");

        var storeFilter = Builders<BsonDocument>.Filter.Eq("storeId", storeId);
        var billDocs = await billsColl.Find(storeFilter).ToListAsync(ct);

        var todayUtc = DateTime.UtcNow.Date;
        var weekStart = todayUtc.AddDays(-7);

        var scopedBills = billDocs
            .Where(d => MatchesScope(d, deviceId, scope))
            .Where(d => MatchesPosCounterFilter(d, posCounterFilter))
            .ToList();

        var (billsToday, revenueToday, billsWeek, revenueWeek) = Summarize(scopedBills, todayUtc, weekStart);

        int? storeBillsToday = null;
        decimal? storeRevenueToday = null;
        if (string.IsNullOrWhiteSpace(posCounterFilter)
            && (scope == ReportScope.StoreWide || scope == ReportScope.ThisCounter))
        {
            var (st, sr, _, _) = Summarize(billDocs, todayUtc, weekStart);
            storeBillsToday = st;
            storeRevenueToday = sr;
        }

        var recent = scopedBills
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
                var pos = ReadPosCounter(x.Doc);
                var dev = x.Doc.TryGetValue("deviceId", out var di) && di.IsString ? di.AsString : "";
                var counterDisplay = CounterDisplayFormatter.Format(pos, dev);
                return new DashboardRecentBill
                {
                    BillNo = billNo,
                    CreatedAtDisplay = x.Dt!.Value.ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC",
                    Payable = payable,
                    CounterDisplay = counterDisplay,
                };
            })
            .ToList();

        long productCount;
        decimal totalAvailableQty;
        try
        {
            var activeFilter = Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Eq("isActive", true),
                Builders<BsonDocument>.Filter.Not(Builders<BsonDocument>.Filter.Exists("isActive")));
            var products = await productsColl.Find(activeFilter)
                .Project(Builders<BsonDocument>.Projection.Include("stockQty"))
                .ToListAsync(ct);
            productCount = products.Count;
            totalAvailableQty = products.Sum(product => ReadDecimal(product, "stockQty"));
        }
        catch
        {
            var products = await productsColl.Find(FilterDefinition<BsonDocument>.Empty)
                .Project(Builders<BsonDocument>.Projection.Include("stockQty"))
                .ToListAsync(ct);
            productCount = products.Count;
            totalAvailableQty = products.Sum(product => ReadDecimal(product, "stockQty"));
        }

        return new StoreDashboardSnapshot
        {
            StoreId = storeId,
            Scope = scope,
            BillsTodayCount = billsToday,
            BillsTodayRevenue = revenueToday,
            BillsLast7DaysCount = billsWeek,
            BillsLast7DaysRevenue = revenueWeek,
            StoreWideBillsTodayCount = storeBillsToday,
            StoreWideBillsTodayRevenue = storeRevenueToday,
            ProductCacheCount = productCount,
            TotalAvailableQty = totalAvailableQty,
            RecentBills = recent,
        };
    }

    private static bool MatchesScope(BsonDocument doc, string? deviceId, ReportScope scope)
    {
        if (scope == ReportScope.StoreWide)
            return true;
        if (string.IsNullOrWhiteSpace(deviceId))
            return false;
        if (!doc.TryGetValue("deviceId", out var v) || !v.IsString)
            return false;
        return string.Equals(v.AsString, deviceId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesPosCounterFilter(BsonDocument doc, string? posCounterFilter)
    {
        if (string.IsNullOrWhiteSpace(posCounterFilter))
            return true;
        var pos = ReadPosCounter(doc);
        return string.Equals(pos, posCounterFilter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadPosCounter(BsonDocument doc)
    {
        if (doc.TryGetValue("posCounter", out var pc) && pc.IsString)
            return pc.AsString.Trim();
        return "";
    }

    private static (int billsToday, decimal revenueToday, int billsWeek, decimal revenueWeek) Summarize(
        IReadOnlyList<BsonDocument> billDocs,
        DateTime todayUtc,
        DateTime weekStart)
    {
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

        return (billsToday, revenueToday, billsWeek, revenueWeek);
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
