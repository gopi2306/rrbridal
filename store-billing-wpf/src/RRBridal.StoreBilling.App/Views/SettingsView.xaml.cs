using System.Windows;
using System.Windows.Controls;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void SettingsView_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            _ = vm.LoadReceiptSettingsAsync(tryPullIfLoggedIn: true);
    }

    private void SettingsTabs_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not TabControl tabs || tabs.SelectedItem is not TabItem tab)
            return;
        if (tab.Header is not string header || !header.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase))
            return;
        if (DataContext is SettingsViewModel vm)
            _ = vm.RefreshWhatsAppStatusAsync();
    }
}
