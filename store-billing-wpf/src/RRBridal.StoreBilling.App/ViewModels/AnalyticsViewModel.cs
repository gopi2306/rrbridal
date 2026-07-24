using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
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

    [ObservableProperty] private string _periodBillsCount = "—";

    [ObservableProperty] private string _periodRevenue = "—";

    [ObservableProperty] private string _periodAvgBill = "—";

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
            TotalsSummary = $"{snap.TotalBillsInPeriod} bills · {MoneyMath.FormatRupee(snap.TotalRevenueInPeriod)}";

            PeriodBillsCount = snap.TotalBillsInPeriod.ToString();
            PeriodRevenue = MoneyMath.FormatRupee(snap.TotalRevenueInPeriod);
            PeriodAvgBill = snap.TotalBillsInPeriod > 0
                ? MoneyMath.FormatRupee(snap.TotalRevenueInPeriod / snap.TotalBillsInPeriod)
                : "—";

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
}
