using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using RRBridal.StoreBilling.App.Models;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class BillingView
{
    public BillingView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WireViewModel(DataContext as BillingViewModel);
        ApplyLineItemColumnVisibility();
    }

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

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.Services.FocusBillingProductSearch = null;
        if (DataContext is BillingViewModel vm)
            vm.RequestFocusEntryProductCode = null;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is BillingViewModel oldVm)
            oldVm.RequestFocusEntryProductCode = null;
        WireViewModel(e.NewValue as BillingViewModel);
    }

    private void WireViewModel(BillingViewModel? vm)
    {
        if (vm == null)
            return;
        vm.RequestFocusEntryProductCode = FocusEntryProductCodeCell;
        App.Services.FocusBillingProductSearch = FocusEntryProductCodeCell;
    }

    private void FocusEntryProductCodeCell()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (DataContext is not BillingViewModel vm)
                return;

            var entry = vm.Lines.FirstOrDefault(l => l.IsEntryRow);
            if (entry == null)
                return;

            LinesGrid.UpdateLayout();
            LinesGrid.SelectedItem = entry;
            LinesGrid.CurrentCell = new DataGridCellInfo(entry, LinesGrid.Columns[1]);
            LinesGrid.BeginEdit();
        }, DispatcherPriority.Input);
    }

    private void LinesGrid_OnPreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (e.Column.Header as string != "Product code")
            return;
        if (e.EditingElement is not TextBox box)
            return;
        if (e.Row.Item is not BillingLineItem line || !line.IsEntryRow)
            return;

        box.KeyDown -= EntryProductCodeBox_OnKeyDown;
        box.KeyDown += EntryProductCodeBox_OnKeyDown;
    }

    private async void EntryProductCodeBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        LinesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);
        if (DataContext is BillingViewModel vm && sender is TextBox box)
            await vm.CommitProductCodeInputAsync(box.Text);
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
