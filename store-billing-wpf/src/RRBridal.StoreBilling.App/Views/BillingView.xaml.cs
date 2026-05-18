using System.Windows.Input;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class BillingView
{
    public BillingView()
    {
        InitializeComponent();
    }

    private void CustomerPhoneBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        if (DataContext is BillingViewModel vm)
            _ = vm.SearchCustomerByPhoneCommand.ExecuteAsync(null);
    }
}
