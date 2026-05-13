using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Inventory;
using RRBridal.StoreBilling.App.Services.Store;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    private readonly StoreDashboardService _dashboardService;
    private readonly InventoryGridClient _inventoryClient;
    private readonly StoreContext _storeContext;

    [ObservableProperty] private string _storeIdDisplay = "";

    [ObservableProperty] private string _billsTodaySummary = "—";

    [ObservableProperty] private string _billsWeekSummary = "—";

    [ObservableProperty] private string _pendingOutboxSummary = "—";

    [ObservableProperty] private string _syncSummary = "—";

    [ObservableProperty] private string _productCacheSummary = "—";

    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty] private string _inventorySearchText = "";

    [ObservableProperty] private string _inventoryHint = "Search SKU, barcode, or product name (central inventory).";

    public ObservableCollection<DashboardRecentBill> RecentBills { get; } = new();

    public ObservableCollection<InventoryGridRow> InventoryRows { get; } = new();

    public DashboardViewModel(AppServices services)
    {
        _dashboardService = new StoreDashboardService(services.LocalDb);
        _inventoryClient = services.InventoryGrid;
        _storeContext = services.StoreContext;
    }

    [RelayCommand]
    private async Task Refresh()
    {
        StatusMessage = "Loading store metrics…";
        try
        {
            var storeId = _storeContext.StoreId;
            StoreIdDisplay = storeId;

            var snap = await _dashboardService.LoadAsync(storeId);

            BillsTodaySummary = $"{snap.BillsTodayCount} bills · {FormatRupee(snap.BillsTodayRevenue)}";
            BillsWeekSummary = $"{snap.BillsLast7DaysCount} bills · {FormatRupee(snap.BillsLast7DaysRevenue)}";
            PendingOutboxSummary = snap.PendingOutboxCount.ToString(InCulture);
            SyncSummary = string.IsNullOrWhiteSpace(snap.SyncUpdatedAt)
                ? $"Cursor {snap.SyncCursor}"
                : $"Cursor {snap.SyncCursor} · last pull {snap.SyncUpdatedAt}";
            ProductCacheSummary = snap.ProductCacheCount.ToString(InCulture);

            RecentBills.Clear();
            foreach (var r in snap.RecentBills)
                RecentBills.Add(r);

            StatusMessage = "Metrics updated " + DateTime.Now.ToString("T", InCulture);
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not load metrics: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task SearchInventory()
    {
        InventoryRows.Clear();
        InventoryHint = "Searching…";
        try
        {
            var storeId = _storeContext.StoreId;
            var rows = await _inventoryClient.SearchAsync(InventorySearchText, storeId, 100);

            foreach (var r in rows)
                InventoryRows.Add(r);

            InventoryHint = rows.Count == 0
                ? "No rows. Try another search or ensure products/ledger exist in central."
                : $"{rows.Count} row(s) for store «{storeId}».";
        }
        catch (Exception ex)
        {
            InventoryHint =
                "Inventory requires central API. Check CENTRAL_API_BASE and login in Settings. " + ex.Message;
        }
    }

    private static string FormatRupee(decimal value) => "₹ " + value.ToString("N2", InCulture);
}
