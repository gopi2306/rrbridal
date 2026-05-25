using System.Windows;
using System.Windows.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Products;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class ProductSearchDialog
{
    public CatalogProduct? SelectedProduct { get; private set; }

    public ProductSearchDialog(string initialQuery, AppServices services, bool codeOnly = false)
    {
        InitializeComponent();
        DataContext = new ProductSearchViewModel(services.ProductCatalog, initialQuery, codeOnly);
        Loaded += async (_, _) =>
        {
            if (DataContext is ProductSearchViewModel vm)
                await vm.SearchCommand.ExecuteAsync(null);
        };
    }

    private void Add_OnClick(object sender, RoutedEventArgs e) => TryClose(true);

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Results_OnMouseDoubleClick(object sender, MouseButtonEventArgs e) => TryClose(true);

    private void TryClose(bool success)
    {
        if (DataContext is not ProductSearchViewModel vm || vm.SelectedProduct == null)
        {
            if (success)
                MessageBox.Show("Select a product first.", "Pick product", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedProduct = vm.SelectedProduct;
        DialogResult = true;
        Close();
    }
}
