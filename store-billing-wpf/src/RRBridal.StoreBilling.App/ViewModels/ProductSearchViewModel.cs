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

    [ObservableProperty] private string _searchQuery = "";

    public ObservableCollection<CatalogProduct> Results { get; } = new();

    [ObservableProperty] private CatalogProduct? _selectedProduct;

    [ObservableProperty] private string _statusText = "Search local cache and central API.";

    public ProductSearchViewModel(ProductCatalogService catalog, string initialQuery)
    {
        _catalog = catalog;
        _searchQuery = initialQuery ?? "";
    }

    [RelayCommand]
    private async Task Search()
    {
        Results.Clear();
        SelectedProduct = null;
        StatusText = "Searching…";
        var items = await _catalog.SearchAsync(SearchQuery, CancellationToken.None);
        foreach (var p in items)
            Results.Add(p);
        StatusText = items.Count == 0
            ? "No products. Run Sync in Settings, or add products in central, or check CENTRAL_API_BASE."
            : $"{items.Count} product(s). Double-click or select and click Add.";
    }
}
