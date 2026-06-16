using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Store;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App.ViewModels;

public sealed class CashMovementRow
{
    public required string MovementNo { get; init; }
    public required string TypeDisplay { get; init; }
    public required string Description { get; init; }
    public required string AmountDisplay { get; init; }
    public required string PostedAtLocal { get; init; }
}

public partial class DayCloseViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");
    private readonly AppServices _services;
    private readonly StoreContext _storeContext;
    private DaySessionRecord? _session;
    private DayBillingCloseSnapshot? _snapshot;

    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _sessionStatusBanner = "Day: Not opened";
    [ObservableProperty] private string _openingCashText = "";
    [ObservableProperty] private bool _canOpenDay = true;
    [ObservableProperty] private bool _canCloseDay;
    [ObservableProperty] private bool _isDayClosed;
    [ObservableProperty] private string _storeDisplayName = "";
    [ObservableProperty] private string _tillDisplayLine = "";

    [ObservableProperty] private string _summaryBillCount = "—";
    [ObservableProperty] private string _summaryAmount = "—";
    [ObservableProperty] private string _summaryNetCash = "—";
    [ObservableProperty] private string _summaryExpectedCash = "—";
    [ObservableProperty] private string _summaryDeposits = "—";
    [ObservableProperty] private string _summaryWithdrawals = "—";
    [ObservableProperty] private string _summaryExpenses = "—";
    [ObservableProperty] private string _summaryExpectedTender = "—";

    [ObservableProperty] private string _movementDescription = "";
    [ObservableProperty] private string _movementAmountText = "";
    [ObservableProperty] private bool _canPostMovements;

    public ObservableCollection<CashMovementRow> CashMovements { get; } = new();

    public DayCloseViewModel(AppServices services)
    {
        _services = services;
        _storeContext = services.StoreContext;
        ApplyBrandingFromShell();
        _ = RefreshCommand.ExecuteAsync(null);
    }

    public void ApplyBrandingFromShell()
    {
        var snap = _services.ShellBranding.Current;
        StoreDisplayName = snap.StoreDisplayName;
        TillDisplayLine = snap.TillDisplayLine;
    }

    [RelayCommand]
    private async Task Refresh()
    {
        StatusMessage = "Loading…";
        try
        {
            var storeId = _storeContext.StoreId;
            var posCounter = _storeContext.PosCounter;
            var businessDate = DaySessionService.FormatBusinessDate(SelectedDate);
            _session = await _services.DaySessions.GetSessionAsync(storeId, businessDate, posCounter);
            _snapshot = await _services.DaySessions.LoadDayCloseWithSessionAsync(
                storeId, SelectedDate, posCounter);

            UpdateSessionUi();
            UpdateSummaryUi(_snapshot);

            var movements = await _services.CashMovements.ListForBusinessDateAsync(
                storeId, businessDate, posCounter);
            CashMovements.Clear();
            foreach (var m in movements)
            {
                CashMovements.Add(new CashMovementRow
                {
                    MovementNo = m.MovementNo,
                    TypeDisplay = string.Equals(m.MovementType, CashMovementType.DepositToBank, StringComparison.OrdinalIgnoreCase)
                        ? "Deposit to bank"
                        : "Cash withdrawal",
                    Description = m.Description,
                    AmountDisplay = MoneyMath.FormatRupee(m.Amount),
                    PostedAtLocal = FormatUtcLocal(m.CreatedAtUtc),
                });
            }

            StatusMessage = $"Updated {DateTime.Now:T}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Load failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task OpenDay()
    {
        if (!decimal.TryParse(OpeningCashText?.Trim(), NumberStyles.Any, InCulture, out var opening) || opening < 0)
        {
            StatusMessage = "Enter a valid opening cash amount.";
            return;
        }

        var user = _services.UserSession?.LoggedInUser.Name ?? "Unknown";
        var (success, message, session) = await _services.DaySessions.OpenDayAsync(opening, user, SelectedDate);
        StatusMessage = message;
        if (success)
        {
            _session = session;
            _services.NotifyDaySessionChanged?.Invoke();
            await Refresh();
        }
    }

    [RelayCommand]
    private async Task PostDeposit() =>
        await PostMovementAsync(CashMovementType.DepositToBank);

    [RelayCommand]
    private async Task PostWithdrawal() =>
        await PostMovementAsync(CashMovementType.CashWithdrawal);

    private async Task PostMovementAsync(string movementType)
    {
        if (!decimal.TryParse(MovementAmountText?.Trim(), NumberStyles.Any, InCulture, out var amount))
        {
            StatusMessage = "Enter a valid movement amount.";
            return;
        }

        var businessDate = DaySessionService.FormatBusinessDate(SelectedDate);
        var (success, message) = await _services.CashMovements.PostMovementAsync(
            movementType,
            MovementDescription,
            amount,
            businessDate);

        StatusMessage = message;
        if (success)
        {
            MovementDescription = "";
            MovementAmountText = "";
            _services.NotifyDaySessionChanged?.Invoke();
            await Refresh();
        }
    }

    [RelayCommand]
    private void OpenCashHandOver()
    {
        if (_snapshot == null)
            return;

        if (_session == null || !string.Equals(_session.Status, DaySessionStatus.Open, StringComparison.OrdinalIgnoreCase))
        {
            if (_session != null && string.Equals(_session.Status, DaySessionStatus.Closed, StringComparison.OrdinalIgnoreCase))
            {
                ShowCashHandOver(readOnly: true);
                return;
            }

            StatusMessage = "Open the day before cash hand over.";
            return;
        }

        ShowCashHandOver(readOnly: false);
    }

    private void ShowCashHandOver(bool readOnly)
    {
        if (_snapshot == null)
            return;

        var vm = new CashHandOverViewModel(
            _services,
            _snapshot,
            _session,
            SelectedDate,
            readOnly,
            closed =>
            {
                if (closed)
                    _ = Refresh();
            });

        var win = new CashHandOverWindow(vm)
        {
            Owner = Application.Current.MainWindow,
        };
        win.ShowDialog();
        _services.NotifyDaySessionChanged?.Invoke();
        _ = Refresh();
    }

    private void UpdateSessionUi()
    {
        if (_session == null)
        {
            SessionStatusBanner = "Day: Not opened";
            CanOpenDay = SelectedDate.Date == DateTime.Today;
            CanCloseDay = false;
            IsDayClosed = false;
            CanPostMovements = false;
            OpeningCashText = "";
            return;
        }

        if (string.Equals(_session.Status, DaySessionStatus.Closed, StringComparison.OrdinalIgnoreCase))
        {
            SessionStatusBanner = $"Day: Closed by {_session.ClosedBy} at {FormatUtcLocal(_session.ClosedAtUtc)}";
            CanOpenDay = false;
            CanCloseDay = false;
            IsDayClosed = true;
            CanPostMovements = false;
            OpeningCashText = _session.OpeningCash.ToString("N2", InCulture);
            return;
        }

        SessionStatusBanner = $"Day: Open since {FormatUtcLocal(_session.OpenedAtUtc)} · Opening {MoneyMath.FormatRupee(_session.OpeningCash)}";
        CanOpenDay = false;
        CanCloseDay = SelectedDate.Date == DateTime.Today;
        IsDayClosed = false;
        CanPostMovements = SelectedDate.Date == DateTime.Today;
        OpeningCashText = _session.OpeningCash.ToString("N2", InCulture);
    }

    private void UpdateSummaryUi(DayBillingCloseSnapshot snap)
    {
        SummaryBillCount = snap.BillCount.ToString(InCulture);
        SummaryAmount = MoneyMath.FormatRupee(snap.TotalAmount);
        SummaryNetCash = MoneyMath.FormatRupee(snap.NetCashInHand);
        SummaryExpectedCash = MoneyMath.FormatRupee(snap.ExpectedCash);
        SummaryDeposits = snap.DepositsTotal > 0 ? MoneyMath.FormatRupee(snap.DepositsTotal) : "—";
        SummaryWithdrawals = snap.WithdrawalsTotal > 0 ? MoneyMath.FormatRupee(snap.WithdrawalsTotal) : "—";
        SummaryExpenses = snap.DailyExpensesTotal > 0 ? MoneyMath.FormatRupee(snap.DailyExpensesTotal) : "—";
        SummaryExpectedTender = MoneyMath.FormatRupee(snap.ActualHandInTotal);
    }

    private static string FormatUtcLocal(string? utc)
    {
        if (string.IsNullOrWhiteSpace(utc))
            return "—";
        if (!DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return utc;
        return dt.ToLocalTime().ToString("dd-MMM-yyyy HH:mm", InCulture);
    }

    partial void OnSelectedDateChanged(DateTime value) => _ = Refresh();
}
