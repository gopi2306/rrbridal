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
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape && DataContext is ShellViewModel shellEscape && shellEscape.IsNavDrawerOpen)
        {
            shellEscape.CloseNavDrawerCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (key != Key.F3)
            return;
        if (DataContext is not ShellViewModel vm)
            return;
        vm.FocusGlobalSearchCommand.Execute(null);
        e.Handled = true;
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
