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
using RRBridal.StoreBilling.App.Services.Store;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class GoingOutOfStockReportViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");
    private readonly AppServices _services;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private StatusFilterOption? _selectedStatusFilter;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private bool _isTruncated;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _statusMessage = "Choose filters and select Search.";
    [ObservableProperty] private string _skuCount = "—";
    [ObservableProperty] private string _criticalCount = "—";
    [ObservableProperty] private string _lowCount = "—";
    [ObservableProperty] private string _zeroQtyCount = "—";

    public ObservableCollection<StatusFilterOption> StatusFilterOptions { get; } = new()
    {
        new(null, "All statuses"),
        new("critical", "Critical"),
        new("low", "Low"),
    };

    public ObservableCollection<GoingOutOfStockRow> Rows { get; } = new();

    public bool HasRows => Rows.Count > 0;
    public string TruncatedMessage { get; private set; } = "";

    public GoingOutOfStockReportViewModel(AppServices services)
    {
        _services = services;
        SelectedStatusFilter = StatusFilterOptions[0];
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
        StatusMessage = "Loading going out of stock report…";
        try
        {
            var response = await _services.GoingOutOfStockReports.LoadAsync(query);
            Rows.Clear();
            foreach (var row in response.Data)
                Rows.Add(row);

            ApplyTotals(response.Totals);
            IsEmpty = Rows.Count == 0;
            IsTruncated = response.Truncated;
            TruncatedMessage = response.Truncated
                ? $"Showing the first {response.Limit:N0} of {response.Total:N0} SKUs. Narrow the filters for complete results."
                : "";
            OnPropertyChanged(nameof(TruncatedMessage));
            OnPropertyChanged(nameof(HasRows));

            var filter = SelectedStatusFilter?.Label ?? "All statuses";
            StatusMessage = IsEmpty
                ? $"No SKUs at or below match qty / product threshold · {filter}."
                : $"{response.Totals.SkuCount:N0} SKU(s) · {filter} · as of {response.Period.AsOf} · updated {DateTime.Now.ToString("T", InCulture)}";
        }
        catch (Exception ex)
        {
            Rows.Clear();
            ResetTotals();
            IsEmpty = true;
            IsTruncated = false;
            HasError = true;
            OnPropertyChanged(nameof(HasRows));
            StatusMessage = "Could not load going out of stock report: " + ex.Message;
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

        var dialog = new SaveFileDialog
        {
            Title = "Save going out of stock report",
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            FileName = $"going-out-of-stock-{DateTime.Today:yyyy-MM-dd}.xlsx",
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
            await _services.GoingOutOfStockReports.SaveExportAsync(dialog.FileName, query);
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

    private bool TryBuildQuery(out GoingOutOfStockReportQuery query)
    {
        query = new GoingOutOfStockReportQuery
        {
            StoreCode = _services.StoreContext.StoreId,
            StoreName = _services.ShellBranding.Current.StoreDisplayName,
            Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            Status = SelectedStatusFilter?.Status,
            Limit = GoingOutOfStockReportService.MaxRows,
        };
        return true;
    }

    private void ApplyTotals(GoingOutOfStockTotals totals)
    {
        SkuCount = totals.SkuCount.ToString("N0", InCulture);
        CriticalCount = totals.CriticalCount.ToString("N0", InCulture);
        LowCount = totals.LowCount.ToString("N0", InCulture);
        ZeroQtyCount = totals.ZeroQtyCount.ToString("N0", InCulture);
    }

    private void ResetTotals()
    {
        SkuCount = CriticalCount = LowCount = ZeroQtyCount = "—";
    }

    private bool CanRun() => !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        SearchCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        DownloadExcelCommand.NotifyCanExecuteChanged();
    }
}

public sealed class StatusFilterOption(string? status, string label)
{
    public string? Status { get; } = status;
    public string Label { get; } = label;
}
