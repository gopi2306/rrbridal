using System.Windows.Input;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class DashboardView
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void InventorySearchBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        if (DataContext is DashboardViewModel vm)
            _ = vm.SearchInventoryCommand.ExecuteAsync(null);
    }
}
