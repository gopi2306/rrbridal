using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using RRBridal.StoreBilling.App.Models;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class BarcodePrintingView
{
    public BarcodePrintingView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) =>
        WireViewModel(DataContext as BarcodePrintingViewModel);

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.Services.FocusBarcodeSkuEntry = null;
        if (DataContext is BarcodePrintingViewModel vm)
            vm.RequestFocusEntryCode = null;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is BarcodePrintingViewModel oldVm)
            oldVm.RequestFocusEntryCode = null;
        WireViewModel(e.NewValue as BarcodePrintingViewModel);
    }

    private void WireViewModel(BarcodePrintingViewModel? vm)
    {
        if (vm == null)
            return;
        vm.RequestFocusEntryCode = FocusEntryCodeCell;
        App.Services.FocusBarcodeSkuEntry = FocusEntryCodeCell;
    }

    private void FocusEntryCodeCell()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (DataContext is not BarcodePrintingViewModel vm)
                return;

            var entry = vm.Lines.FirstOrDefault(l => l.IsDraftRow);
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
        if (e.Column.Header as string != "Code")
            return;
        if (e.EditingElement is not TextBox box)
            return;
        if (e.Row.Item is not BarcodePrintLineItem line || !line.IsDraftRow)
            return;

        box.KeyDown -= EntryCodeBox_OnKeyDown;
        box.KeyDown += EntryCodeBox_OnKeyDown;
    }

    private async void EntryCodeBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        LinesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);
        if (DataContext is BarcodePrintingViewModel vm && sender is TextBox box)
            await vm.CommitCodeInputAsync(box.Text);
    }

    private void BarcodePrintingView_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not BarcodePrintingViewModel vm)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        switch (key)
        {
            case Key.F5:
                vm.PrintLabelsCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F6:
                vm.OpenItemListCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F7:
                vm.ClearScreenCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
