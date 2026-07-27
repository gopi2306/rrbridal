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

public sealed class StoreAnalyticsService
{
    private readonly IMongoDatabase _db;
    private CentralOnlineModeService? _centralMode;
    private CentralDashboardClient? _dashboardApi;

    public StoreAnalyticsService(IMongoDatabase localDb)
    {
        _db = localDb;
    }

    public void ConfigureOnline(CentralOnlineModeService centralMode, CentralDashboardClient dashboardApi)
    {
        _centralMode = centralMode;
        _dashboardApi = dashboardApi;
    }

    private bool IsCentralOnline => _centralMode?.IsOnlineMode == true && _dashboardApi != null;

    /// <summary>
    /// Builds a day-by-day sales view from posted <c>store_bills</c> for the given store (UTC days).
    /// </summary>
    public async Task<StoreAnalyticsSnapshot> LoadAsync(string storeId, int dayCount = 14, CancellationToken ct = default)
    {
        dayCount = Math.Clamp(dayCount, 1, 90);

        if (IsCentralOnline)
            return await LoadOnlineAsync(dayCount, ct);

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

    private async Task<StoreAnalyticsSnapshot> LoadOnlineAsync(int dayCount, CancellationToken ct)
    {
        var today = DateTime.Today;
        var startDate = today.AddDays(-(dayCount - 1));
        var from = startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var to = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        using var json = await _dashboardApi!.GetStoreSalesAsync("custom", from, to, billPage: 1, billLimit: 1, ct: ct);
        var root = json.RootElement;

        var periodLabel = root.TryGetProperty("period", out var periodEl)
            ? CentralDashboardClient.ReadString(periodEl, "label", $"{from} to {to}")
            : $"{from} to {to}";

        var summaryInvoices = 0;
        var summaryNet = 0m;
        if (root.TryGetProperty("summary", out var summaryEl))
        {
            summaryInvoices = CentralDashboardClient.ReadInt(summaryEl, "invoices");
            summaryNet = CentralDashboardClient.ReadDecimal(summaryEl, "netSales");
        }

        var rows = new List<DailySalesRow>();
        if (root.TryGetProperty("salesDetails", out var detailsEl) && detailsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in detailsEl.EnumerateArray())
            {
                var bucketKey = CentralDashboardClient.ReadString(el, "bucketKey");
                var label = CentralDashboardClient.ReadString(el, "label", bucketKey);
                var dayUtc = DateTime.TryParse(
                    bucketKey, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                    ? DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc)
                    : DateTime.MinValue;

                rows.Add(new DailySalesRow
                {
                    DayUtc = dayUtc,
                    DayLabel = label,
                    BillsCount = CentralDashboardClient.ReadInt(el, "invoices"),
                    Revenue = CentralDashboardClient.ReadDecimal(el, "net"),
                });
            }
        }

        rows = rows.OrderByDescending(r => r.DayUtc).ToList();

        return new StoreAnalyticsSnapshot
        {
            PeriodLabel = $"{periodLabel} (Online)",
            TotalBillsInPeriod = summaryInvoices,
            TotalRevenueInPeriod = summaryNet,
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
