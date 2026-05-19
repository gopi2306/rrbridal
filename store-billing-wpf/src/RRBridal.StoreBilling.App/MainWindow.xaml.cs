using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using RRBridal.StoreBilling.App.Services;
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
        if (DataContext is ShellViewModel shell)
        {
            shell.NavigateCommand.Execute(ShellPage.Billing);
            shell.EnsurePageVisibilityFresh();
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key != Key.F3)
            return;
        if (DataContext is not ShellViewModel vm)
            return;
        vm.FocusGlobalSearchCommand.Execute(null);
        e.Handled = true;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.Services.FocusSearch = this;
        GlobalSearchBox.KeyDown += GlobalSearchBox_OnKeyDown;
        if (DataContext is ShellViewModel shell)
        {
            shell.EnsurePageVisibilityFresh();
            await shell.RefreshBrandingAsync();
        }
    }

    private async void GlobalSearchBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        if (DataContext is not ShellViewModel shell)
            return;

        if (shell.CurrentPage == ShellPage.Billing)
        {
            shell.Billing.SearchText = shell.GlobalSearchText;
            await shell.Billing.OpenProductSearchAsync();
            shell.GlobalSearchText = shell.Billing.SearchText;
        }
        else if (shell.CurrentPage == ShellPage.SaleReturn)
        {
            await shell.SaleReturn.AddExchangeProductFromSearchAsync(shell.GlobalSearchText);
            shell.GlobalSearchText = "";
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(App.Services.FocusSearch, this))
            App.Services.FocusSearch = null;
    }

    public void FocusGlobalSearch()
    {
        GlobalSearchBox.Focus();
        Keyboard.Focus(GlobalSearchBox);
    }
}
