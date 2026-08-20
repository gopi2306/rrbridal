using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Store;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class SupplierWiseReportViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");
    private readonly AppServices _services;

    [ObservableProperty] private DateTime _dateFrom = DateTime.Today;
    [ObservableProperty] private DateTime _dateTo = DateTime.Today;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private PosCounterFilterOption? _selectedPosCounterFilter;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private bool _isTruncated;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _statusMessage = "Choose filters and select Search.";
    [ObservableProperty] private string _supplierCount = "—";
    [ObservableProperty] private string _skuCount = "—";
    [ObservableProperty] private string _soldQty = "—";
    [ObservableProperty] private string _returnQty = "—";
    [ObservableProperty] private string _netQty = "—";
    [ObservableProperty] private string _netAmount = "—";

    public ObservableCollection<PosCounterFilterOption> PosCounterFilterOptions { get; } = new();
    public ObservableCollection<SupplierWiseSupplierRow> SupplierRows { get; } = new();

    public bool HasRows => SupplierRows.Count > 0;
    public string TruncatedMessage { get; private set; } = "";

    public SupplierWiseReportViewModel(AppServices services)
    {
        _services = services;
        ReloadCounterOptions();
    }

    public void ReloadCounterOptions()
    {
        var previous = SelectedPosCounterFilter?.PosCounter;
        var counters = (_services.PosBillingSettings.Current.ScreenAccess?.KnownCounters ?? [])
            .Append(_services.StoreContext.PosCounter)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => int.TryParse(value, out var number) ? number : int.MaxValue)
            .ThenBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        PosCounterFilterOptions.Clear();
        PosCounterFilterOptions.Add(new PosCounterFilterOption(null, "All counters"));
        foreach (var counter in counters)
            PosCounterFilterOptions.Add(new PosCounterFilterOption(counter, $"POS {counter}"));

        SelectedPosCounterFilter = PosCounterFilterOptions.FirstOrDefault(option =>
            string.Equals(option.PosCounter, previous, StringComparison.OrdinalIgnoreCase))
            ?? PosCounterFilterOptions[0];
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task Refresh() => Search();

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task Search()
    {
        if (!TryBuildQuery(out var query))
            return;

        IsBusy = true;
        HasError = false;
        StatusMessage = "Loading supplier-wise sales report…";
        try
        {
            var response = await _services.SkuSalesReports.LoadSupplierWiseAsync(query);
            SupplierRows.Clear();
            foreach (var row in response.Data)
                SupplierRows.Add(row);

            ApplyTotals(response.Totals);
            IsEmpty = SupplierRows.Count == 0;
            IsTruncated = response.Truncated;
            TruncatedMessage = response.Truncated
                ? $"Showing the first {response.Limit:N0} of {response.Total:N0} matching bills. Narrow the filters for complete results."
                : "";
            OnPropertyChanged(nameof(TruncatedMessage));
            OnPropertyChanged(nameof(HasRows));

            var filter = SelectedPosCounterFilter?.Label ?? "All counters";
            StatusMessage = IsEmpty
                ? $"No posted supplier sales found from {DateFrom:dd MMM yyyy} to {DateTo:dd MMM yyyy} · {filter}."
                : $"{response.Totals.SupplierCount:N0} supplier(s), {response.Totals.SkuCount:N0} SKU(s) · {filter} · updated {DateTime.Now.ToString("T", InCulture)}";
        }
        catch (Exception ex)
        {
            SupplierRows.Clear();
            ResetTotals();
            IsEmpty = true;
            IsTruncated = false;
            HasError = true;
            OnPropertyChanged(nameof(HasRows));
            StatusMessage = "Could not load supplier-wise report: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task DownloadExcel()
    {
        if (!TryBuildQuery(out var query))
            return;

        var counterSuffix = string.IsNullOrWhiteSpace(query.PosCounter) ? "" : $"-pos{query.PosCounter}";
        var dialog = new SaveFileDialog
        {
            Title = "Save supplier-wise sales report",
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            FileName = $"supplier-wise-{query.From:yyyy-MM-dd}-to-{query.To:yyyy-MM-dd}{counterSuffix}.xlsx",
            AddExtension = true,
            DefaultExt = ".xlsx",
        };
        if (dialog.ShowDialog(Application.Current.MainWindow) != true)
            return;

        IsBusy = true;
        HasError = false;
        StatusMessage = "Preparing Excel workbook…";
        try
        {
            await _services.SkuSalesReports.SaveSupplierWiseExportAsync(dialog.FileName, query);
            StatusMessage = "Excel report saved to " + dialog.FileName;
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = "Could not download Excel report: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryBuildQuery(out SkuSalesReportQuery query)
    {
        query = null!;
        if (DateFrom.Date > DateTo.Date)
        {
            HasError = true;
            StatusMessage = "From date must be on or before To date.";
            return false;
        }

        query = new SkuSalesReportQuery
        {
            From = DateFrom.Date,
            To = DateTo.Date,
            StoreCode = _services.StoreContext.StoreId,
            StoreName = _services.ShellBranding.Current.StoreDisplayName,
            Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            PosCounter = SelectedPosCounterFilter?.PosCounter,
            Limit = SkuSalesReportService.MaxRows,
        };
        return true;
    }

    private void ApplyTotals(SkuSalesTotals totals)
    {
        SupplierCount = totals.SupplierCount.ToString("N0", InCulture);
        SkuCount = totals.SkuCount.ToString("N0", InCulture);
        SoldQty = totals.SoldQty.ToString("N2", InCulture);
        ReturnQty = totals.ReturnQty.ToString("N2", InCulture);
        NetQty = totals.NetQty.ToString("N2", InCulture);
        NetAmount = FormatMoney(totals.NetAmount);
    }

    private void ResetTotals()
    {
        SupplierCount = SkuCount = SoldQty = ReturnQty = NetQty = NetAmount = "—";
    }

    private static string FormatMoney(decimal value) => value.ToString("C2", InCulture);
    private bool CanRun() => !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        SearchCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        DownloadExcelCommand.NotifyCanExecuteChanged();
    }
}
