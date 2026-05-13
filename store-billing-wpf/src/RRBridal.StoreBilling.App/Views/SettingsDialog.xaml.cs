using System.Windows;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class SettingsDialog : Window
{
    public SettingsDialog(AppServices services)
    {
        InitializeComponent();
        DataContext = new SettingsViewModel(services);
    }

    private void SettingsDialog_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.LoadReceiptSettings();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
