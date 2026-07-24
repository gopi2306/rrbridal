using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Ui;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App;

public partial class MainWindow : Window, IFocusSearchService
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ShellViewModel(App.Services);
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        Loaded += OnLoaded;
        Closed += OnClosed;
        SizeChanged += MainWindow_SizeChanged;
        if (DataContext is ShellViewModel shell)
        {
            shell.NavigateCommand.Execute(ShellPage.Billing);
            shell.EnsurePageVisibilityFresh();
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ShellViewModel vm)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;

        if (key == Key.Escape && vm.IsNavDrawerOpen)
        {
            vm.CloseNavDrawerCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (key == Key.F3 && modifiers == ModifierKeys.None)
        {
            vm.FocusGlobalSearchCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Ctrl chords handled here so TextBox does not swallow Ctrl+A / Ctrl+F / etc.
        if ((modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            return;
        if ((modifiers & (ModifierKeys.Alt | ModifierKeys.Windows)) != ModifierKeys.None)
            return;

        var shift = (modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        switch (key)
        {
            case Key.G when !shift:
                vm.ToggleNavDrawerCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.A when !shift:
                vm.NavigateCommand.Execute(ShellPage.Billing);
                e.Handled = true;
                break;
            case Key.Q when !shift:
                vm.NavigateCommand.Execute(ShellPage.QuotationManagement);
                e.Handled = true;
                break;
            case Key.B when !shift:
                vm.NavigateCommand.Execute(ShellPage.Barcodes);
                e.Handled = true;
                break;
            case Key.D when !shift:
                vm.NavigateCommand.Execute(ShellPage.Dashboard);
                e.Handled = true;
                break;
            case Key.Y when !shift:
                vm.NavigateCommand.Execute(ShellPage.Analytics);
                e.Handled = true;
                break;
            case Key.O when !shift:
                vm.NavigateCommand.Execute(ShellPage.OnlineSales);
                e.Handled = true;
                break;
            case Key.I when !shift:
                vm.NavigateCommand.Execute(ShellPage.CreditBills);
                e.Handled = true;
                break;
            case Key.U when !shift:
                vm.NavigateCommand.Execute(ShellPage.Customers);
                e.Handled = true;
                break;
            case Key.M when !shift:
                vm.NavigateCommand.Execute(ShellPage.Salesmen);
                e.Handled = true;
                break;
            case Key.L when !shift:
                vm.NavigateCommand.Execute(ShellPage.Ledger);
                e.Handled = true;
                break;
            case Key.R when !shift:
                vm.NavigateCommand.Execute(ShellPage.SaleReturn);
                e.Handled = true;
                break;
            case Key.K when !shift:
                vm.NavigateCommand.Execute(ShellPage.BillLookup);
                e.Handled = true;
                break;
            case Key.W when !shift:
                vm.NavigateCommand.Execute(ShellPage.DayClose);
                e.Handled = true;
                break;
            case Key.J when !shift:
                vm.NavigateCommand.Execute(ShellPage.DuplicateBill);
                e.Handled = true;
                break;
            case Key.T when !shift:
                vm.NavigateCommand.Execute(ShellPage.Adjustments);
                e.Handled = true;
                break;
            case Key.E when !shift:
                vm.NavigateCommand.Execute(ShellPage.DailyExpenses);
                e.Handled = true;
                break;
            case Key.OemComma when !shift:
                vm.NavigateCommand.Execute(ShellPage.Settings);
                e.Handled = true;
                break;
            case Key.N when !shift:
                vm.ClearForNewBillCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F when !shift:
                vm.FocusGlobalSearchCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.H when !shift:
                vm.HoldBillCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.P when !shift:
                vm.PrintStubCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Enter when !shift:
                if (vm.PostBillCommand.CanExecute(null))
                    vm.PostBillCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.S when !shift:
                vm.SaveCustomerRegistrationCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        WindowLayoutHelper.ApplyStartupBounds(this);
        if (DataContext is ShellViewModel shellLayout)
            shellLayout.UpdateShellLayout(ActualWidth);

        App.Services.FocusSearch = this;
        if (DataContext is ShellViewModel shell)
        {
            shell.EnsurePageVisibilityFresh();
            await shell.RefreshBrandingAsync();
            await shell.RefreshNotificationCountAsync();
            shell.RequestBillingSearchFocus();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(App.Services.FocusSearch, this))
            App.Services.FocusSearch = null;
    }

    public void FocusBillingProductSearch()
    {
        App.Services.FocusBillingProductSearch?.Invoke();
    }

    public void FocusBarcodeSkuEntry()
    {
        App.Services.FocusBarcodeSkuEntry?.Invoke();
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is ShellViewModel shell)
            shell.UpdateShellLayout(e.NewSize.Width);
    }

    private void NavDrawerOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ShellViewModel shell)
            shell.CloseNavDrawerCommand.Execute(null);
    }
}
