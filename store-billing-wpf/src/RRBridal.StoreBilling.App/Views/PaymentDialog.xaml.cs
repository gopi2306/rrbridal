using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class PaymentDialog : Window
{
    public PaymentDialogViewModel ViewModel { get; }

    public PaymentDialog(PaymentDialogViewModel vm)
    {
        InitializeComponent();
        ViewModel = vm;
        DataContext = vm;
        vm.CloseDialog = success => { DialogResult = success; };
        Loaded += (_, _) => DialogLayoutHelper.CenterAndClamp(this, Owner);
    }
}
