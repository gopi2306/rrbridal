using System.Windows;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class NotificationsDialog : Window
{
    private readonly NotificationsViewModel _vm;

    public NotificationsDialog(AppServices services)
    {
        InitializeComponent();
        _vm = new NotificationsViewModel(services);
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.LoadAsync();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
