using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Models;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.BarcodePrinting;
using RRBridal.StoreBilling.App.Services.Products;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class BarcodePrintingViewModel : ObservableObject
{
    private readonly AppServices _services;

    public ObservableCollection<BarcodePrintLineItem> Lines { get; } = new();

    [ObservableProperty] private string _storeDisplayName = "";

    [ObservableProperty] private string _statusMessage = "Type SKU in the blank row and press Enter, or use F6 to pick a product.";

    public Action? RequestFocusEntryCode { get; set; }

    public BarcodePrintingViewModel(AppServices services)
    {
        _services = services;
        ApplyBrandingFromShell();
        ResetLines();
    }

    public void ApplyBrandingFromShell()
    {
        StoreDisplayName = _services.ShellBranding.Current.StoreDisplayName;
    }

    private void ResetLines()
    {
        Lines.Clear();
        Lines.Add(BarcodePrintLineItem.CreateDraftRow());
        RenumberLines();
    }

    private void RenumberLines()
    {
        var n = 1;
        foreach (var line in Lines.Where(l => !l.IsDraftRow))
            line.LineNo = n++;
    }

    private BarcodePrintLineItem? GetDraftRow() =>
        Lines.FirstOrDefault(l => l.IsDraftRow);

    [RelayCommand]
    private void ClearScreen()
    {
        ResetLines();
        StatusMessage = "List cleared.";
        RequestFocusEntryCode?.Invoke();
    }

    [RelayCommand]
    private async Task OpenItemListAsync()
    {
        var dlg = new ProductSearchDialog("", _services, codeOnly: false)
        {
            Owner = Application.Current.MainWindow,
        };
        if (dlg.ShowDialog() == true && dlg.SelectedProduct != null)
            AddFromCatalog(dlg.SelectedProduct);
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task PrintLabelsAsync()
    {
        var printable = Lines.Where(l => !l.IsDraftRow && l.PrintQty > 0).ToList();
        if (printable.Count == 0)
        {
            MessageBox.Show(
                "Enter quantity on at least one line before printing.",
                "Barcode printing",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _services.ReceiptConfig.Reload();
        var company = _services.ReceiptConfig.Current.Store.StoreName?.Trim();
        if (string.IsNullOrWhiteSpace(company))
            company = _services.ShellBranding.Current.CompanyTitle;

        var preview = new BarcodeLabelPrintPreviewWindow(printable, company ?? "RR Bridal", _services.BarcodeLabelDesign)
        {
            Owner = Application.Current.MainWindow,
        };
        preview.ShowDialog();

        if (!string.IsNullOrWhiteSpace(preview.LastResultMessage))
            StatusMessage = preview.LastResultMessage;

        await Task.CompletedTask;
    }

    public async Task CommitCodeInputAsync(string? input, CancellationToken ct = default)
    {
        var q = (input ?? "").Trim();
        if (q.Length < 1)
        {
            MessageBox.Show(
                "Enter a product code (SKU or barcode).",
                "Barcode printing",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var exact = await _services.ProductCatalog.FindBySkuOrBarcodeAsync(q, ct);
        if (exact != null)
        {
            AddFromCatalog(exact);
            return;
        }

        var codeItems = await _services.ProductCatalog.SearchByProductCodeAsync(q, ct);
        if (codeItems.Count == 1)
        {
            AddFromCatalog(codeItems[0]);
            return;
        }

        if (codeItems.Count > 1)
        {
            var codeDlg = new ProductSearchDialog(q, _services, codeOnly: true)
            {
                Owner = Application.Current.MainWindow,
            };
            if (codeDlg.ShowDialog() == true && codeDlg.SelectedProduct != null)
                AddFromCatalog(codeDlg.SelectedProduct);
            return;
        }

        var nameItems = await _services.ProductCatalog.SearchAsync(q, ct);
        if (nameItems.Count > 0)
        {
            var nameDlg = new ProductSearchDialog(q, _services, codeOnly: false)
            {
                Owner = Application.Current.MainWindow,
            };
            if (nameDlg.ShowDialog() == true && nameDlg.SelectedProduct != null)
                AddFromCatalog(nameDlg.SelectedProduct);
            return;
        }

        MessageBox.Show(
            $"No product found in local catalog for \"{q}\".",
            "Barcode printing",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        ClearDraftCode();
        RequestFocusEntryCode?.Invoke();
    }

    private void AddFromCatalog(CatalogProduct product)
    {
        var existing = Lines.FirstOrDefault(l =>
            !l.IsDraftRow && string.Equals(l.Code, product.Sku, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.PrintQty += 1;
            StatusMessage = $"Increased qty for {product.Sku} to {existing.PrintQty:0.##}.";
            ClearDraftCode();
            RequestFocusEntryCode?.Invoke();
            return;
        }

        foreach (var d in Lines.Where(l => l.IsDraftRow).ToList())
            Lines.Remove(d);

        var lineNo = Lines.Count + 1;
        Lines.Add(BarcodePrintLineItem.FromCatalog(product, lineNo));
        Lines.Add(BarcodePrintLineItem.CreateDraftRow());
        RenumberLines();
        StatusMessage = $"Added {product.Sku} — {product.Name}.";
        ClearDraftCode();
        RequestFocusEntryCode?.Invoke();
    }

    private void ClearDraftCode()
    {
        var draft = GetDraftRow();
        if (draft != null)
            draft.Code = "";
    }
}
