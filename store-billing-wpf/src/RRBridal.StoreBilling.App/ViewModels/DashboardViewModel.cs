using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Inventory;
using RRBridal.StoreBilling.App.Services.Store;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    private readonly StoreDashboardService _dashboardService;
    private readonly InventoryGridClient _inventoryClient;
    private readonly StoreContext _storeContext;
    private readonly ShellBrandingService _shellBranding;
    private bool _suppressFilterRefresh;

    [ObservableProperty] private string _storeIdDisplay = "";

    [ObservableProperty] private string _storeDisplayName = "";

    [ObservableProperty] private string _tillDisplayLine = "";

    [ObservableProperty] private ReportScope _reportScope = ReportScope.StoreWide;

    [ObservableProperty] private string _storeWideTodaySummary = "";

    [ObservableProperty] private string _billsTodaySummary = "—";

    [ObservableProperty] private string _billsWeekSummary = "—";

    [ObservableProperty] private string _pendingOutboxSummary = "—";

    [ObservableProperty] private string _syncSummary = "—";

    [ObservableProperty] private string _productCacheSummary = "—";

    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty] private string _inventorySearchText = "";

    [ObservableProperty] private InventoryStockFilter _inventoryStockFilter = InventoryStockFilter.All;

    [ObservableProperty] private string _inventoryHint = "Search SKU, barcode, or product name in local store inventory.";

    [ObservableProperty] private PosCounterFilterOption? _selectedPosCounterFilter;

    public bool IsPrimaryCounter => _storeContext.IsPrimaryCounter;

    public ObservableCollection<PosCounterFilterOption> PosCounterFilterOptions { get; } = new();

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
        _shellBranding = services.ShellBranding;
        PosCounterFilterOptions.Add(new PosCounterFilterOption(null, "All counters"));
        SelectedPosCounterFilter = PosCounterFilterOptions[0];
        ApplyBrandingFromShell();
    }

    public void ApplyBrandingFromShell()
    {
        StoreDisplayName = _shellBranding.Current.StoreDisplayName;
        TillDisplayLine = _shellBranding.Current.TillDisplayLine;
    }

    [RelayCommand]
    private async Task Refresh()
    {
        StatusMessage = "Loading store metrics…";
        try
        {
            var storeId = _storeContext.StoreId;
            StoreIdDisplay = storeId;
            ApplyBrandingFromShell();

            await ReloadPosCounterFilterOptionsAsync(storeId);

            var posFilter = SelectedPosCounterFilter?.PosCounter;
            var scope = ReportScope.StoreWide;
            var snap = await _dashboardService.LoadAsync(
                storeId,
                scope,
                _storeContext.DeviceId,
                posFilter);

            BillsTodaySummary = $"{snap.BillsTodayCount} bills · {MoneyMath.FormatRupee(snap.BillsTodayRevenue)}";
            StoreWideTodaySummary = string.IsNullOrWhiteSpace(posFilter) && snap.StoreWideBillsTodayCount is int st
                ? $"Store-wide today: {st} bills · {MoneyMath.FormatRupee(snap.StoreWideBillsTodayRevenue ?? 0)}"
                : "";
            BillsWeekSummary = $"{snap.BillsLast7DaysCount} bills · {MoneyMath.FormatRupee(snap.BillsLast7DaysRevenue)}";
            PendingOutboxSummary = snap.PendingOutboxCount.ToString(InCulture);
            SyncSummary = string.IsNullOrWhiteSpace(snap.SyncUpdatedAt)
                ? $"Cursor {snap.SyncCursor}"
                : $"Cursor {snap.SyncCursor} · last pull {snap.SyncUpdatedAt}";
            ProductCacheSummary = snap.ProductCacheCount.ToString(InCulture);

            RecentBills.Clear();
            foreach (var r in snap.RecentBills)
                RecentBills.Add(r);

            var filterLabel = SelectedPosCounterFilter?.Label ?? "All counters";
            StatusMessage = $"Metrics updated {DateTime.Now.ToString("T", InCulture)} · {filterLabel}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not load metrics: " + ex.Message;
        }
    }

    partial void OnSelectedPosCounterFilterChanged(PosCounterFilterOption? value)
    {
        if (!_suppressFilterRefresh)
            _ = Refresh();
    }

    partial void OnInventoryStockFilterChanged(InventoryStockFilter value)
    {
        InventoryPage = 1;
        _ = LoadInventoryGridAsync();
    }

    private async Task ReloadPosCounterFilterOptionsAsync(string storeId)
    {
        var previous = SelectedPosCounterFilter?.PosCounter;
        var distinct = await _dashboardService.GetDistinctPosCountersAsync(storeId);

        _suppressFilterRefresh = true;
        try
        {
            PosCounterFilterOptions.Clear();
            PosCounterFilterOptions.Add(new PosCounterFilterOption(null, "All counters"));
            foreach (var p in distinct)
                PosCounterFilterOptions.Add(new PosCounterFilterOption(p, $"POS{p}"));

            SelectedPosCounterFilter = PosCounterFilterOptions.FirstOrDefault(o =>
                string.Equals(o.PosCounter, previous, StringComparison.OrdinalIgnoreCase))
                ?? PosCounterFilterOptions[0];
        }
        finally
        {
            _suppressFilterRefresh = false;
        }
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

    private static string GetStockFilterLabel(InventoryStockFilter filter) =>
        filter switch
        {
            InventoryStockFilter.InStock => "In stock only",
            InventoryStockFilter.OutOfStock => "Out of stock only",
            _ => "All products",
        };
}
