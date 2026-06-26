using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class OnlineSalesView
{
    private ShellViewModel? _shell;

    public OnlineSalesView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => HookShell();

    private void OnUnloaded(object sender, RoutedEventArgs e) => UnhookShell();

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            RestoreSelectionAndRefreshCommand();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        RestoreSelectionAndRefreshCommand();
    }

    private void HookShell()
    {
        if (_shell is not null)
            return;

        if (Window.GetWindow(this)?.DataContext is ShellViewModel shell)
        {
            _shell = shell;
            _shell.PropertyChanged += Shell_OnPropertyChanged;
        }
    }

    private void UnhookShell()
    {
        if (_shell is null)
            return;

        _shell.PropertyChanged -= Shell_OnPropertyChanged;
        _shell = null;
    }

    private void Shell_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.LayoutBreakpoint)
            or nameof(ShellViewModel.ShellWidth)
            or nameof(ShellViewModel.IsCompactLayout))
        {
            Dispatcher.BeginInvoke(RestoreSelectionAndRefreshCommand);
        }
    }

    private void ResultsGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RefreshRecordPaymentCommand();

    private void RestoreSelectionAndRefreshCommand()
    {
        if (DataContext is not OnlineSalesViewModel vm)
            return;

        if (vm.SelectedOrder is not null && ResultsGrid.SelectedItem != vm.SelectedOrder)
            ResultsGrid.SelectedItem = vm.SelectedOrder;

        RefreshRecordPaymentCommand();
    }

    private void RefreshRecordPaymentCommand()
    {
        if (DataContext is OnlineSalesViewModel vm)
            vm.RecordPaymentCommand.NotifyCanExecuteChanged();
    }
}
