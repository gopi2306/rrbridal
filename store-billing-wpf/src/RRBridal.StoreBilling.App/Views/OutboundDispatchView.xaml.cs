using System.Windows.Input;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class OutboundDispatchView
{
    public OutboundDispatchView()
    {
        InitializeComponent();
    }

    private async void BillSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not OutboundDispatchViewModel vm) return;
        e.Handled = true;
        await vm.SearchBillsCommand.ExecuteAsync(null);
    }

    private async void BillResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not OutboundDispatchViewModel vm || vm.SelectedBill == null) return;
        await vm.LoadSelectedBillCommand.ExecuteAsync(null);
    }

    private void Dispatches_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is OutboundDispatchViewModel vm && vm.SelectedDispatch != null)
            vm.OpenSelectedDispatchCommand.Execute(null);
    }
}
