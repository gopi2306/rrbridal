using System.ComponentModel;
using System.Windows;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class CustomersView
{
    public CustomersView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => FocusNewCustomerName();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        if (e.NewValue is INotifyPropertyChanged newVm)
            newVm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CustomersViewModel.IsNewCustomer)
            or nameof(CustomersViewModel.IsDetailReadOnly)
            or nameof(CustomersViewModel.ShowDetailForm))
        {
            Dispatcher.BeginInvoke(FocusNewCustomerName);
        }
    }

    private async void GstinBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is CustomersViewModel vm)
            await vm.LookupGstinOnLostFocusCommand.ExecuteAsync(null);
    }

    private void FocusNewCustomerName()
    {
        if (DataContext is not CustomersViewModel vm
            || !vm.IsNewCustomer
            || vm.IsDetailReadOnly
            || !CustomerNameInput.IsVisible)
            return;

        // Never pull focus away from a field the user is already typing in.
        if (DetailForm.IsKeyboardFocusWithin)
            return;

        CustomerNameInput.Focus();
        if (string.IsNullOrEmpty(CustomerNameInput.Text))
            return;
        CustomerNameInput.CaretIndex = CustomerNameInput.Text.Length;
    }
}
