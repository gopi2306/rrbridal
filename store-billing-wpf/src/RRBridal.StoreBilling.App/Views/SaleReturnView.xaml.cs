using System.Windows;
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

    private void LegacyCustomerPhoneBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        CommitLegacyPhoneSearch();
    }

    private void LegacyCustomerPhoneBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        CommitLegacyPhoneSearch();
    }

    private void CommitLegacyPhoneSearch()
    {
        if (DataContext is SaleReturnViewModel vm)
            _ = vm.HandleLegacyPhoneCommittedAsync();
    }

    private async void LegacyCustomerNameBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        if (DataContext is SaleReturnViewModel vm)
            await vm.SearchLegacyCustomerByNameAsync();
    }
}
