using System.Collections.Specialized;
using System.Globalization;
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
    private static readonly CultureInfo ParseCulture = CultureInfo.InvariantCulture;

    private BillingViewModel? _wiredVm;
    private bool _snapEntryProductCode;

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
        RequestEntryProductCodeFocus();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            ApplyLineItemColumnVisibility();
            RequestEntryProductCodeFocus();
        }
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
        UnwireViewModel();
        App.Services.FocusBillingProductSearch = null;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UnwireViewModel();
        WireViewModel(e.NewValue as BillingViewModel);
    }

    private void UnwireViewModel()
    {
        if (_wiredVm == null)
            return;

        _wiredVm.Lines.CollectionChanged -= OnLinesCollectionChanged;
        _wiredVm.RequestFocusEntryProductCode = null;
        _wiredVm = null;
    }

    private void WireViewModel(BillingViewModel? vm)
    {
        if (vm == null)
            return;

        _wiredVm = vm;
        vm.Lines.CollectionChanged += OnLinesCollectionChanged;
        vm.RequestFocusEntryProductCode = RequestEntryProductCodeFocus;
        App.Services.FocusBillingProductSearch = RequestEntryProductCodeFocus;
    }

    private void OnLinesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is not (NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset))
            return;

        if (_wiredVm?.Lines.Any(l => l.IsEntryRow) != true)
            return;

        RequestEntryProductCodeFocus();
    }

    private void RequestEntryProductCodeFocus()
    {
        _snapEntryProductCode = true;
        Dispatcher.BeginInvoke(SafeEndEdit, DispatcherPriority.Input);
        Dispatcher.BeginInvoke(() => FocusEntryCell("Product code"), DispatcherPriority.Loaded);
        Dispatcher.BeginInvoke(() => FocusEntryCell("Product code"), DispatcherPriority.ApplicationIdle);
    }

    private void LinesGrid_OnCurrentCellChanged(object sender, EventArgs e)
    {
        if (!_snapEntryProductCode)
            return;

        if (!IsEntryRowCurrentCell(out var header))
            return;

        if (header == "Product code")
        {
            _snapEntryProductCode = false;
            return;
        }

        RequestEntryProductCodeFocus();
    }

    private DataGridColumn? FindColumn(string header) =>
        LinesGrid.Columns.FirstOrDefault(c => string.Equals(c.Header as string, header, System.StringComparison.Ordinal));

    private bool IsEntryRowCurrentCell(out string? header)
    {
        header = LinesGrid.CurrentCell.Column?.Header as string;
        return LinesGrid.CurrentCell.Item is BillingLineItem line && line.IsEntryRow;
    }

    private void SafeEndEdit()
    {
        try { LinesGrid.CommitEdit(DataGridEditingUnit.Cell, true); }
        catch (InvalidOperationException) { }

        try { LinesGrid.CommitEdit(DataGridEditingUnit.Row, true); }
        catch (InvalidOperationException) { }

        try { LinesGrid.CancelEdit(DataGridEditingUnit.Cell); }
        catch (InvalidOperationException) { }

        try { LinesGrid.CancelEdit(DataGridEditingUnit.Row); }
        catch (InvalidOperationException) { }
    }

    private void FocusEntryCell(string header)
    {
        if (_wiredVm == null)
            return;

        var column = FindColumn(header);
        if (column == null)
            return;

        var entry = _wiredVm.Lines.FirstOrDefault(l => l.IsEntryRow);
        if (entry == null)
            return;

        try
        {
            SafeEndEdit();
            LinesGrid.UpdateLayout();
            LinesGrid.CurrentCell = new DataGridCellInfo(entry, column);
            LinesGrid.ScrollIntoView(entry, column);
            LinesGrid.Focus();

            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (LinesGrid.CurrentCell.IsValid
                        && LinesGrid.CurrentCell.Item is BillingLineItem line
                        && line.IsEntryRow
                        && string.Equals(LinesGrid.CurrentCell.Column?.Header as string, header, System.StringComparison.Ordinal))
                    {
                        LinesGrid.BeginEdit();
                    }
                }
                catch (InvalidOperationException) { }
            }, DispatcherPriority.Input);
        }
        catch (InvalidOperationException) { }
    }

    private void LinesGrid_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (!IsEntryRowCurrentCell(out var header))
            return;

        if (header != "Qty")
            return;

        if (Keyboard.FocusedElement is TextBox)
            return;

        e.Handled = true;
        SafeEndEdit();
        RequestEntryProductCodeFocus();
    }

    private void LinesGrid_OnPreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (e.EditingElement is not TextBox box)
            return;
        if (e.Row.Item is not BillingLineItem line || !line.IsEntryRow)
            return;

        var header = e.Column.Header as string;
        if (header == "Product code")
        {
            box.PreviewKeyDown -= EntryProductCodeBox_OnPreviewKeyDown;
            box.PreviewKeyDown += EntryProductCodeBox_OnPreviewKeyDown;
            return;
        }

        if (header != "Qty")
            return;

        box.PreviewKeyDown -= EntryQtyBox_OnPreviewKeyDown;
        box.PreviewKeyDown += EntryQtyBox_OnPreviewKeyDown;
        box.GotFocus -= EntryQtyBox_OnGotFocus;
        box.GotFocus += EntryQtyBox_OnGotFocus;
    }

    private void EntryQtyBox_OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box)
            return;
        if (box.Text is "0" or "0.0" or "0.00")
            box.Text = "";
        box.SelectAll();
    }

    private void EntryQtyBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        var text = (sender as TextBox)?.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(text) || text is "0" or "0.0" or "0.00")
        {
            SafeEndEdit();
            RequestEntryProductCodeFocus();
            return;
        }

        if (!decimal.TryParse(text, NumberStyles.Number, ParseCulture, out var qty) || qty <= 0)
        {
            MessageBox.Show(
                "Enter a valid quantity greater than zero.",
                "RR Bridal Billing",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Dispatcher.BeginInvoke(() => FocusEntryCell("Qty"), DispatcherPriority.Input);
            return;
        }

        SafeEndEdit();
        RequestEntryProductCodeFocus();
    }

    private async void EntryProductCodeBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        SafeEndEdit();
        if (_wiredVm != null && sender is TextBox box)
            await _wiredVm.CommitProductCodeInputAsync(box.Text);
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
        if (_wiredVm != null)
            _ = _wiredVm.HandlePhoneCommittedAsync();
    }

    private async void CustomerNameBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        if (_wiredVm != null)
            await _wiredVm.SearchCustomerByNameAsync();
    }
}
