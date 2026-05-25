using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services.Products;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class ProductSearchViewModel : ObservableObject
{
    private readonly ProductCatalogService _catalog;
    private readonly bool _codeOnly;

    [ObservableProperty] private string _searchQuery = "";

    public ObservableCollection<CatalogProduct> Results { get; } = new();

    [ObservableProperty] private CatalogProduct? _selectedProduct;

    [ObservableProperty] private string _statusText = "Search local cache and central API.";

    public string HeaderText =>
        _codeOnly
            ? "Search product code (SKU, barcode, alias)"
            : "Search product name (store cache)";

    public ProductSearchViewModel(ProductCatalogService catalog, string initialQuery, bool codeOnly = false)
    {
        _catalog = catalog;
        _codeOnly = codeOnly;
        _searchQuery = initialQuery ?? "";
        StatusText = _codeOnly
            ? "Enter SKU, barcode, or alias."
            : "Search local cache by product name.";
    }

    [RelayCommand]
    private async Task Search()
    {
        Results.Clear();
        SelectedProduct = null;
        StatusText = "Searching…";
        var items = _codeOnly
            ? await _catalog.SearchByProductCodeAsync(SearchQuery, CancellationToken.None)
            : await _catalog.SearchAsync(SearchQuery, CancellationToken.None);
        foreach (var p in items)
            Results.Add(p);
        StatusText = items.Count == 0
            ? _codeOnly
                ? "No products for that code. Run Sync in Settings or check the code."
                : "No products. Run Sync in Settings, or add products in central, or check CENTRAL_API_BASE."
            : $"{items.Count} product(s). Double-click or select and click Add.";
    }
}
