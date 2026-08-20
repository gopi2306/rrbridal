using CommunityToolkit.Mvvm.ComponentModel;
using RRBridal.StoreBilling.App.Services;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class ReportsViewModel : ObservableObject
{
    public CustomerBillingReportViewModel CustomerBilling { get; }
    public SupplierWiseReportViewModel SupplierWise { get; }
    public FastSellersReportViewModel FastSellers { get; }
    public GoingOutOfStockReportViewModel GoingOutOfStock { get; }

    [ObservableProperty] private int _selectedTabIndex;

    public ReportsViewModel(AppServices services)
    {
        CustomerBilling = new CustomerBillingReportViewModel(services);
        SupplierWise = new SupplierWiseReportViewModel(services);
        FastSellers = new FastSellersReportViewModel(services);
        GoingOutOfStock = new GoingOutOfStockReportViewModel(services);
    }

    public void OnNavigated() => LoadSelectedTab();

    partial void OnSelectedTabIndexChanged(int value) => LoadSelectedTab();

    private void LoadSelectedTab()
    {
        switch (SelectedTabIndex)
        {
            case 0:
                CustomerBilling.ReloadCounterOptions();
                if (CustomerBilling.RefreshCommand.CanExecute(null))
                    _ = CustomerBilling.RefreshCommand.ExecuteAsync(null);
                break;
            case 1:
                SupplierWise.ReloadCounterOptions();
                if (SupplierWise.RefreshCommand.CanExecute(null))
                    _ = SupplierWise.RefreshCommand.ExecuteAsync(null);
                break;
            case 2:
                FastSellers.ReloadCounterOptions();
                if (FastSellers.RefreshCommand.CanExecute(null))
                    _ = FastSellers.RefreshCommand.ExecuteAsync(null);
                break;
            case 3:
                if (GoingOutOfStock.RefreshCommand.CanExecute(null))
                    _ = GoingOutOfStock.RefreshCommand.ExecuteAsync(null);
                break;
        }
    }
}
