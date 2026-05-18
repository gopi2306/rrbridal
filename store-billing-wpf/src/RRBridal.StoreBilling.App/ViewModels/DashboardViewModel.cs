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

    [ObservableProperty] private InventoryStockFilter _inventoryStockFilter = InventoryStockFilter.All;

    [ObservableProperty] private string _inventoryHint = "Search SKU, barcode, or product name in local store inventory.";

    public IReadOnlyList<InventoryStockFilterOption> StockFilterOptions { get; } =
    [
        new(InventoryStockFilter.All, "All products"),
        new(InventoryStockFilter.InStock, "In stock only"),
        new(InventoryStockFilter.OutOfStock, "Out of stock only"),
    ];

    [ObservableProperty] private int _inventoryPage = 1;

    [ObservableProperty] private int _inventoryPageSize = 100;

    [ObservableProperty] private int _inventoryTotal;

    [ObservableProperty] private int _inventoryTotalPages;

    [ObservableProperty] private string _inventoryPagerLabel = "";

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

    partial void OnInventoryStockFilterChanged(InventoryStockFilter value)
    {
        InventoryPage = 1;
        _ = LoadInventoryGridAsync();
    }

    [RelayCommand]
    private async Task SearchInventory()
    {
        InventoryPage = 1;
        await LoadInventoryGridAsync();
    }

    [RelayCommand]
    private async Task InventoryNextPage()
    {
        if (InventoryTotalPages == 0 || InventoryPage >= InventoryTotalPages)
            return;
        InventoryPage++;
        await LoadInventoryGridAsync();
    }

    [RelayCommand]
    private async Task InventoryPreviousPage()
    {
        if (InventoryPage <= 1)
            return;
        InventoryPage--;
        await LoadInventoryGridAsync();
    }

    private async Task LoadInventoryGridAsync()
    {
        InventoryRows.Clear();
        InventoryHint = "Loading…";
        InventoryPagerLabel = "";
        try
        {
            var storeId = _storeContext.StoreId;
            var result = await _inventoryClient.SearchAsync(
                InventorySearchText,
                storeId,
                InventoryPage,
                InventoryPageSize,
                InventoryStockFilter);

            foreach (var r in result.Data)
                InventoryRows.Add(r);

            InventoryTotal = result.Total;
            InventoryTotalPages = result.TotalPages;

            if (result.Total == 0)
            {
                InventoryHint = "No local inventory rows. Sync products/transfers or try another search.";
                InventoryPagerLabel = "";
            }
            else
            {
                InventoryPagerLabel = $"Page {result.Page} of {result.TotalPages} ({result.Total} matches)";
                InventoryHint =
                    $"{result.Data.Count} row(s) on this page · filter: {GetStockFilterLabel(InventoryStockFilter)} · store «{storeId}».";
            }
        }
        catch (Exception ex)
        {
            InventoryHint = "Could not read local store inventory. " + ex.Message;
            InventoryPagerLabel = "";
        }
    }

    private static string FormatRupee(decimal value) => "₹ " + value.ToString("N2", InCulture);

    private static string GetStockFilterLabel(InventoryStockFilter filter) =>
        filter switch
        {
            InventoryStockFilter.InStock => "In stock only",
            InventoryStockFilter.OutOfStock => "Out of stock only",
            _ => "All products",
        };
}
