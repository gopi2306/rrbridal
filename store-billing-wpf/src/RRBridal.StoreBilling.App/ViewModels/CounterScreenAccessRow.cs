using CommunityToolkit.Mvvm.ComponentModel;

namespace RRBridal.StoreBilling.App.ViewModels;

/// <summary>One counter row in the admin screen-access matrix (all Go To screens).</summary>
public partial class CounterScreenAccessRow : ObservableObject
{
    public CounterScreenAccessRow(string posCounter)
    {
        PosCounter = posCounter;
        IsAdminCounter = string.Equals(posCounter, "1", System.StringComparison.OrdinalIgnoreCase);
        // POS 1 always has Settings; other counters follow the saved matrix.
        Settings = IsAdminCounter;
    }

    public string PosCounter { get; }

    public string CounterLabel => $"POS {PosCounter}";

    /// <summary>POS 1 — Settings access cannot be removed for this row.</summary>
    public bool IsAdminCounter { get; }

    /// <summary>Settings may be enabled for any counter; POS 1 checkbox stays locked on.</summary>
    public bool CanEditSettings => !IsAdminCounter;

    [ObservableProperty] private bool _billing = true;
    [ObservableProperty] private bool _vouchers = true;
    [ObservableProperty] private bool _quotations = true;
    [ObservableProperty] private bool _barcodes = true;
    [ObservableProperty] private bool _dashboard;
    [ObservableProperty] private bool _analytics;
    [ObservableProperty] private bool _onlineSales;
    [ObservableProperty] private bool _creditBills;
    [ObservableProperty] private bool _customers = true;
    [ObservableProperty] private bool _salesman = true;
    [ObservableProperty] private bool _ledger;
    [ObservableProperty] private bool _returns = true;
    [ObservableProperty] private bool _billLookup = true;
    [ObservableProperty] private bool _dayClose = true;
    [ObservableProperty] private bool _duplicate = true;
    [ObservableProperty] private bool _adjustments = true;
    [ObservableProperty] private bool _dailyExpenses;
    [ObservableProperty] private bool _settings;
}
