using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class VoucherGatewayView
{
    public VoucherGatewayView()
    {
        InitializeComponent();
    }

    private void VoucherGatewayView_OnLoaded(object sender, RoutedEventArgs e)
    {
        Focus();
        VoucherList.Focus();
    }

    private void VoucherList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is VoucherGatewayViewModel vm)
            vm.OpenSelectedCommand.Execute(null);
    }

    private void VoucherGatewayView_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not VoucherGatewayViewModel vm)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key is Key.Enter or Key.Return)
        {
            vm.OpenSelectedCommand.Execute(null);
            e.Handled = true;
            return;
        }

        var digit = key switch
        {
            Key.D1 or Key.NumPad1 => "1",
            Key.D2 or Key.NumPad2 => "2",
            Key.D3 or Key.NumPad3 => "3",
            Key.D4 or Key.NumPad4 => "4",
            Key.D5 or Key.NumPad5 => "5",
            Key.D6 or Key.NumPad6 => "6",
            _ => null,
        };

        if (digit != null)
        {
            vm.SelectByShortcutCommand.Execute(digit);
            e.Handled = true;
        }
    }
}
