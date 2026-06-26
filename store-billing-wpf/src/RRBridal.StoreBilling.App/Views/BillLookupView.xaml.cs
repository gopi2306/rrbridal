using System.Windows;
using System.Windows.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class BillLookupView
{
    public BillLookupView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => ApplyLineItemColumnVisibility();

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            ApplyLineItemColumnVisibility();
    }

    private void ApplyLineItemColumnVisibility()
    {
        App.Services.PosBillingSettings.Load();
        var level = App.Services.PosBillingSettings.Current.LineItemDetailLevel;
        var showStandard = level >= BillingLineItemDetailLevel.Standard;
        var showFull = level >= BillingLineItemDetailLevel.Full;

        ColHsn.Visibility = showStandard ? Visibility.Visible : Visibility.Collapsed;
        ColDisc.Visibility = showStandard ? Visibility.Visible : Visibility.Collapsed;
        ColMrp.Visibility = showStandard ? Visibility.Visible : Visibility.Collapsed;
        ColTaxPct.Visibility = showStandard ? Visibility.Visible : Visibility.Collapsed;
        ColTaxAmt.Visibility = showStandard ? Visibility.Visible : Visibility.Collapsed;

        ColCashDisc.Visibility = showFull ? Visibility.Visible : Visibility.Collapsed;
        ColScheme.Visibility = showFull ? Visibility.Visible : Visibility.Collapsed;
        ColOrigTax.Visibility = showFull ? Visibility.Visible : Visibility.Collapsed;
        ColRevisedAmt.Visibility = showFull ? Visibility.Visible : Visibility.Collapsed;
        ColRevisedGst.Visibility = showFull ? Visibility.Visible : Visibility.Collapsed;
        ColCgst.Visibility = showFull ? Visibility.Visible : Visibility.Collapsed;
        ColSgst.Visibility = showFull ? Visibility.Visible : Visibility.Collapsed;
        ColIgst.Visibility = showFull ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void BillNoBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (DataContext is not BillLookupViewModel vm)
            return;

        e.Handled = true;
        await vm.SearchBillsCommand.ExecuteAsync(null);
    }

    private async void SearchResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not BillLookupViewModel vm)
            return;

        if (vm.SelectedSearchBill == null)
            return;

        await vm.OpenSelectedBillCommand.ExecuteAsync(null);
    }
}
