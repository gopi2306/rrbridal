using System.Windows.Input;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class AdjustmentBillView
{
    public AdjustmentBillView()
    {
        InitializeComponent();
    }

    private async void SearchResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not AdjustmentBillViewModel vm || vm.SelectedSearchBill == null)
            return;

        await vm.OpenSelectedBillCommand.ExecuteAsync(null);
    }
}
