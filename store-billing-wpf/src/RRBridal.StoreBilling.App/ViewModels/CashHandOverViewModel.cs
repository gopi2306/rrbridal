using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Store;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class CashHandOverViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");
    private readonly AppServices _services;
    private readonly DayBillingCloseSnapshot _snapshot;
    private readonly DaySessionRecord? _session;
    private readonly string _businessDate;
    private readonly DateTime _localDate;
    private readonly Action<bool>? _onClosed;
    public event Action? RequestClose;

    [ObservableProperty] private string _counterDisplay = "";
    [ObservableProperty] private string _userDisplay = "";
    [ObservableProperty] private string _dateDisplay = "";
    [ObservableProperty] private string _cashInHandSummary = "₹ 0.00";
    [ObservableProperty] private string _morningCashSummary = "₹ 0.00";
    [ObservableProperty] private string _expectedCashSummary = "₹ 0.00";
    [ObservableProperty] private string _differenceSummary = "₹ 0.00";
    [ObservableProperty] private string _statusLabel = "";
    [ObservableProperty] private string _cashTaken = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isReadOnly;

    public ObservableCollection<CashDenominationRow> Denominations { get; } = new();

    public CashHandOverViewModel(
        AppServices services,
        DayBillingCloseSnapshot snapshot,
        DaySessionRecord? session,
        DateTime localDate,
        bool isReadOnly,
        Action<bool>? onClosed = null)
    {
        _services = services;
        _snapshot = snapshot;
        _session = session;
        _localDate = localDate.Date;
        _businessDate = DaySessionService.FormatBusinessDate(localDate);
        _onClosed = onClosed;
        IsReadOnly = isReadOnly;

        CounterDisplay = $"POS{services.StoreContext.PosCounter}";
        UserDisplay = services.UserSession?.LoggedInUser.Name ?? "";
        DateDisplay = localDate.ToString("dd/MM/yyyy", InCulture);
        MorningCashSummary = MoneyMath.FormatRupee(snapshot.OpeningCash);
        ExpectedCashSummary = MoneyMath.FormatRupee(snapshot.ExpectedCash);

        foreach (var d in CashDenominationDefaults.StandardDenominations)
            Denominations.Add(new CashDenominationRow(d));

        if (session?.CashDenominations.Count > 0)
        {
            foreach (var row in Denominations)
            {
                var match = session.CashDenominations.FirstOrDefault(x => x.Denomination == row.Denomination);
                if (match != null)
                    row.UnitCountText = match.UnitCount.ToString(InCulture);
            }
        }

        foreach (var row in Denominations)
            row.PropertyChanged += (_, _) => RecalculateTotals();

        CashTaken = session?.CashTaken ?? "";
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        var lines = Denominations.Select(d => new CashDenominationLine
        {
            Denomination = d.Denomination,
            UnitCount = d.UnitCount,
        }).ToList();

        var cashInHand = CashDenominationDefaults.SumDenominations(lines);
        var diff = cashInHand - _snapshot.ExpectedCash;
        CashInHandSummary = MoneyMath.FormatRupee(cashInHand);
        DifferenceSummary = MoneyMath.FormatRupee(diff);
        StatusLabel = CashHandOverPrintFlow.ResolveStatusLabel(diff);
    }

    [RelayCommand]
    private async Task Print()
    {
        var lines = BuildDenominationLines();
        var cashInHand = CashDenominationDefaults.SumDenominations(lines);
        var input = CashHandOverPrintFlow.BuildInput(
            _services,
            lines,
            cashInHand,
            _snapshot.OpeningCash,
            _snapshot.ExpectedCash,
            DateDisplay,
            string.IsNullOrWhiteSpace(CashTaken) ? null : CashTaken.Trim());

        var printed = await CashHandOverPrintFlow.ShowAsync(_services, input);
        if (printed && _session != null && string.Equals(_session.Status, DaySessionStatus.Closed, StringComparison.OrdinalIgnoreCase))
        {
            await _services.DaySessions.MarkCashHandOverPrintedAsync(
                _services.StoreContext.StoreId,
                _businessDate,
                _services.StoreContext.PosCounter);
        }

        StatusMessage = printed ? "Printed." : "Print cancelled.";
    }

    [RelayCommand]
    private async Task CloseDay()
    {
        if (IsReadOnly)
            return;

        var lines = BuildDenominationLines();
        var cashInHand = CashDenominationDefaults.SumDenominations(lines);
        if (cashInHand <= 0)
        {
            StatusMessage = "Enter denomination counts before closing.";
            return;
        }

        var confirm = AppDialog.Show(
            $"Close day with cash in hand {MoneyMath.FormatRupee(cashInHand)}?\nExpected: {MoneyMath.FormatRupee(_snapshot.ExpectedCash)}",
            "Close day",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        var user = _services.UserSession?.LoggedInUser.Name ?? "Unknown";
        var (success, message, _) = await _services.DaySessions.CloseDayAsync(
            lines,
            cashInHand,
            user,
            notes: null,
            cashTaken: string.IsNullOrWhiteSpace(CashTaken) ? null : CashTaken.Trim(),
            localDate: _localDate);

        StatusMessage = message;
        if (success)
        {
            _onClosed?.Invoke(true);
            _services.NotifyDaySessionChanged?.Invoke();
            RequestClose?.Invoke();
        }
    }

    [RelayCommand]
    private void Exit()
    {
        _onClosed?.Invoke(false);
        RequestClose?.Invoke();
    }

    private IReadOnlyList<CashDenominationLine> BuildDenominationLines() =>
        Denominations.Select(d => new CashDenominationLine
        {
            Denomination = d.Denomination,
            UnitCount = d.UnitCount,
        }).ToList();
}
