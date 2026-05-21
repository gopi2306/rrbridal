using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class BillingView
{
    public BillingView()
    {
        InitializeComponent();
    }

    private void EditableAmountBox_OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box)
            return;
        if (box.Text is "0" or "0.0" or "0.00")
            box.Text = "";
        box.SelectAll();
    }

    private void CustomerPhoneBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        CommitPhoneSearch();
    }

    private void CustomerPhoneBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        CommitPhoneSearch();
    }

    private void CommitPhoneSearch()
    {
        if (DataContext is BillingViewModel vm)
            _ = vm.HandlePhoneCommittedAsync();
    }

    private async void CustomerNameBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        if (DataContext is BillingViewModel vm)
            await vm.SearchCustomerByNameAsync();
    }
}
