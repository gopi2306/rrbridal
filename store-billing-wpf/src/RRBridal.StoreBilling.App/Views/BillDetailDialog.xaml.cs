using System.Windows;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class BillDetailDialog : Window
{
    public BillDetailDialog(BillDetailDialogViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.RequestClose = Close;
    }
}
