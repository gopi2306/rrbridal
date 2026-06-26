using System.Windows.Input;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class SaleReturnView
{
    public SaleReturnView()
    {
        InitializeComponent();
    }

    private async void SearchResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not SaleReturnViewModel vm || vm.SelectedSearchBill == null)
            return;

        await vm.OpenSelectedBillCommand.ExecuteAsync(null);
    }
}
