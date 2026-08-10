using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Inventory;

public sealed record PhysicalInventoryPreviewLine(
    int RowNumber,
    string Sku,
    string ItemName,
    decimal QtyBefore,
    decimal NewQty,
    decimal QtyDelta,
    bool Unchanged,
    string? Warning = null);

public sealed record PhysicalInventoryImportError(
    int RowNumber,
    string? Sku,
    string Message);

public sealed record PhysicalInventoryImportPreview(
    IReadOnlyList<PhysicalInventoryPreviewLine> Lines,
    IReadOnlyList<PhysicalInventoryImportError> Errors)
{
    public int Adjusted => Lines.Count(line => !line.Unchanged);
    public int Skipped => Lines.Count(line => line.Unchanged);
    public bool CanCommit => Errors.Count == 0 && Adjusted > 0;
}

public sealed class PhysicalInventoryExcelService
{
    private static readonly string[] Headers = ["SKU", "Item Name", "Phy Qty"];
    private readonly HttpClient _centralApi;
    private readonly StoreContext _storeContext;
    private readonly InventoryGridClient _inventory;
    private CentralOnlineModeService? _centralMode;

    public PhysicalInventoryExcelService(
        HttpClient centralApi,
        StoreContext storeContext,
        InventoryGridClient inventory)
    {
        _centralApi = centralApi;
        _storeContext = storeContext;
        _inventory = inventory;
    }

    public void ConfigureOnline(CentralOnlineModeService centralMode) => _centralMode = centralMode;

    public async Task SaveTemplateAsync(string filePath, CancellationToken ct = default)
    {
        if (_centralMode?.IsOnlineMode == true)
        {
            var url =
                $"/api/inventory-adjustments/import/excel/template?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}";
            using var response = await _centralApi.GetAsync(url, ct).ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var message = System.Text.Encoding.UTF8.GetString(bytes);
                throw new InvalidOperationException(
                    $"Template download failed ({(int)response.StatusCode}): {message}");
            }
            await File.WriteAllBytesAsync(filePath, bytes, ct).ConfigureAwait(false);
            return;
        }

        var products = await _inventory.GetAllAsync(_storeContext.StoreId, ct).ConfigureAwait(false);
        using var workbook = BuildTemplateWorkbook(products);
        workbook.SaveAs(filePath);
    }

    public async Task<PhysicalInventoryImportPreview> ParseAndPreviewAsync(
        string filePath,
        CancellationToken ct = default)
    {
        var inventory = await _inventory.GetAllAsync(_storeContext.StoreId, ct).ConfigureAwait(false);
        return ParseWorkbook(filePath, inventory, ct);
    }

    public static PhysicalInventoryImportPreview ParseWorkbook(
        string filePath,
        IReadOnlyList<InventoryGridRow> inventory,
        CancellationToken ct = default)
    {
        var bySku = inventory.ToDictionary(row => row.Sku, StringComparer.OrdinalIgnoreCase);
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.FirstOrDefault()
                        ?? throw new InvalidOperationException("Excel workbook has no worksheets.");
        var headerRow = worksheet.FirstRowUsed()
                        ?? throw new InvalidOperationException("Excel workbook has no header row.");
        var indexes = ReadHeaderIndexes(headerRow);
        var lines = new List<PhysicalInventoryPreviewLine>();
        var errors = new List<PhysicalInventoryImportError>();
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in worksheet.RowsUsed().Where(row => row.RowNumber() > headerRow.RowNumber()))
        {
            ct.ThrowIfCancellationRequested();
            var sku = row.Cell(indexes.Sku).GetString().Trim();
            var itemName = indexes.ItemName > 0 ? row.Cell(indexes.ItemName).GetString().Trim() : "";
            var qtyText = row.Cell(indexes.PhyQty).GetFormattedString().Trim();
            if (string.IsNullOrWhiteSpace(qtyText))
                continue;
            if (string.IsNullOrWhiteSpace(sku))
            {
                errors.Add(new PhysicalInventoryImportError(row.RowNumber(), null, "SKU is required."));
                continue;
            }
            if (!decimal.TryParse(qtyText, NumberStyles.Number, CultureInfo.InvariantCulture, out var newQty)
                && !decimal.TryParse(qtyText, NumberStyles.Number, CultureInfo.CurrentCulture, out newQty))
            {
                errors.Add(new PhysicalInventoryImportError(
                    row.RowNumber(),
                    sku,
                    "Phy Qty must be numeric."));
                continue;
            }
            if (newQty < 0)
            {
                errors.Add(new PhysicalInventoryImportError(
                    row.RowNumber(),
                    sku,
                    "Phy Qty must be zero or greater."));
                continue;
            }
            if (seen.TryGetValue(sku, out var firstRow))
            {
                errors.Add(new PhysicalInventoryImportError(
                    row.RowNumber(),
                    sku,
                    $"Duplicate SKU; first used on row {firstRow}."));
                continue;
            }
            seen[sku] = row.RowNumber();
            if (!bySku.TryGetValue(sku, out var current))
            {
                errors.Add(new PhysicalInventoryImportError(
                    row.RowNumber(),
                    sku,
                    "SKU was not found in the current store catalog."));
                continue;
            }

            var warning = !string.IsNullOrWhiteSpace(itemName)
                          && !string.Equals(itemName, current.Product, StringComparison.OrdinalIgnoreCase)
                ? $"Item Name differs from current catalog: {current.Product}"
                : null;
            lines.Add(new PhysicalInventoryPreviewLine(
                row.RowNumber(),
                sku,
                current.Product,
                current.StoreQty,
                newQty,
                newQty - current.StoreQty,
                newQty == current.StoreQty,
                warning));
        }

        if (lines.Count == 0 && errors.Count == 0)
            errors.Add(new PhysicalInventoryImportError(0, null, "Enter at least one Phy Qty value."));
        return new PhysicalInventoryImportPreview(lines, errors);
    }

    public static XLWorkbook BuildTemplateWorkbook(IEnumerable<InventoryGridRow> products)
    {
        var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Physical Inventory");
        for (var column = 0; column < Headers.Length; column++)
            sheet.Cell(1, column + 1).Value = Headers[column];
        var rowNumber = 2;
        foreach (var product in products.OrderBy(row => row.Sku, StringComparer.OrdinalIgnoreCase))
        {
            sheet.Cell(rowNumber, 1).Value = product.Sku;
            sheet.Cell(rowNumber, 2).Value = product.Product;
            sheet.Cell(rowNumber, 3).Value = "";
            rowNumber++;
        }
        var header = sheet.Range(1, 1, 1, Headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.LightBlue;
        sheet.SheetView.FreezeRows(1);
        sheet.Column(1).Width = 16;
        sheet.Column(2).Width = 44;
        sheet.Column(3).Width = 14;
        return workbook;
    }

    private static (int Sku, int ItemName, int PhyQty) ReadHeaderIndexes(IXLRow header)
    {
        var sku = 0;
        var itemName = 0;
        var phyQty = 0;
        foreach (var cell in header.CellsUsed())
        {
            var normalized = new string(cell.GetString()
                .Trim()
                .ToLowerInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray());
            switch (normalized)
            {
                case "sku":
                    sku = cell.Address.ColumnNumber;
                    break;
                case "itemname":
                case "productname":
                    itemName = cell.Address.ColumnNumber;
                    break;
                case "phyqty":
                case "physicalqty":
                case "physicalquantity":
                case "newqty":
                    phyQty = cell.Address.ColumnNumber;
                    break;
            }
        }
        if (sku == 0 || phyQty == 0)
            throw new InvalidOperationException("Excel headers must include SKU and Phy Qty.");
        return (sku, itemName, phyQty);
    }
}
