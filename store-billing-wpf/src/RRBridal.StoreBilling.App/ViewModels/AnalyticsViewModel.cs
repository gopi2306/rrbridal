using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Store;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class AnalyticsViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    private readonly StoreAnalyticsService _analyticsService;
    private readonly StoreContext _storeContext;

    [ObservableProperty] private string _storeIdDisplay = "";

    [ObservableProperty] private string _periodSummary = "—";

    [ObservableProperty] private string _totalsSummary = "—";

    [ObservableProperty] private string _statusMessage = "";

    public ObservableCollection<DailySalesRow> DailyRows { get; } = new();

    public AnalyticsViewModel(AppServices services)
    {
        _analyticsService = new StoreAnalyticsService(services.LocalDb);
        _storeContext = services.StoreContext;
    }

    [RelayCommand]
    private async Task Refresh()
    {
        StatusMessage = "Loading sales analytics…";
        try
        {
            var storeId = _storeContext.StoreId;
            StoreIdDisplay = storeId;

            var snap = await _analyticsService.LoadAsync(storeId, dayCount: 14);
            PeriodSummary = snap.PeriodLabel;
            TotalsSummary = $"{snap.TotalBillsInPeriod} bills · {FormatRupee(snap.TotalRevenueInPeriod)}";

            DailyRows.Clear();
            foreach (var r in snap.DailyRows)
                DailyRows.Add(r);

            StatusMessage = "Analytics updated " + DateTime.Now.ToString("T", InCulture);
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not load analytics: " + ex.Message;
        }
    }

    private static string FormatRupee(decimal value) => "₹ " + value.ToString("N2", InCulture);
}
