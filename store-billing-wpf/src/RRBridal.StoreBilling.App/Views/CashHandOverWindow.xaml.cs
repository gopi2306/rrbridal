using System.Windows;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class CashHandOverWindow : Window
{
    public CashHandOverWindow(CashHandOverViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.RequestClose += () =>
        {
            DialogResult = true;
            Close();
        };
    }
}
