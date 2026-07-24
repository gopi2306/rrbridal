using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Products;
using RRBridal.StoreBilling.App.Services.Ui;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class ProductSearchDialog
{
    public CatalogProduct? SelectedProduct { get; private set; }

    public ProductSearchDialog(string initialQuery, AppServices services, bool codeOnly = false)
    {
        InitializeComponent();
        BuildPickListColumns(services);
        DataContext = new ProductSearchViewModel(services.ProductCatalog, initialQuery, codeOnly);
        Loaded += async (_, _) =>
        {
            DialogLayoutHelper.CenterAndClamp(this, Owner);
            if (DataContext is ProductSearchViewModel vm)
                await vm.SearchCommand.ExecuteAsync(null);
        };
    }

    private void BuildPickListColumns(AppServices services)
    {
        services.PosBillingSettings.Load();
        var level = services.PosBillingSettings.Current.LineItemDetailLevel;

        ResultsGridView.Columns.Clear();
        ResultsGridView.Columns.Add(TextColumn("SKU", "Sku", 110));
        ResultsGridView.Columns.Add(TextColumn("Name", "Name", level >= BillingLineItemDetailLevel.Standard ? 180 : 220));
        ResultsGridView.Columns.Add(TextColumn("Rate", "SuggestedRate", 80, "₹ {0:N2}"));
        ResultsGridView.Columns.Add(TextColumn("Image note", "PrimaryImageDescription", 140));

        if (level >= BillingLineItemDetailLevel.Standard)
        {
            ResultsGridView.Columns.Add(TextColumn("GST %", "SuggestedTaxPercent", 60, "{0:N0}"));
            ResultsGridView.Columns.Add(TextColumn("MRP", "Mrp", 80, "₹ {0:N2}"));
            ResultsGridView.Columns.Add(TextColumn("Stock", "StockQty", 60, "{0:N0}"));
        }

        if (level >= BillingLineItemDetailLevel.Full)
            ResultsGridView.Columns.Add(TextColumn("HSN", "HsnSac", 90));
    }

    private static GridViewColumn TextColumn(string header, string path, double width, string? format = null)
    {
        var binding = new Binding(path);
        if (!string.IsNullOrEmpty(format))
            binding.StringFormat = format;

        return new GridViewColumn
        {
            Header = header,
            Width = width,
            DisplayMemberBinding = binding,
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
                AppDialog.Show("Select a product first.", "Pick product", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedProduct = vm.SelectedProduct;
        DialogResult = true;
        Close();
    }
}
