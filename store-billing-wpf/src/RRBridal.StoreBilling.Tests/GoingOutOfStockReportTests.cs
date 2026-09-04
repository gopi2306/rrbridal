using System.Collections.Generic;
using System.Linq;
using RRBridal.StoreBilling.App.Services.Store;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public sealed class GoingOutOfStockReportTests
{
    [Fact]
    public void Evaluate_applies_threshold_priority_critical_low_and_missing_qty()
    {
        var products = new[]
        {
            new GoingOutOfStockReportEvaluator.ProductInput(
                "SKU-CRIT", "Critical item", 5, 2, null, "sup-1", "Acme"),
            new GoingOutOfStockReportEvaluator.ProductInput(
                "SKU-LOW", "Low item", 5, 1, null, "sup-1", "Acme"),
            new GoingOutOfStockReportEvaluator.ProductInput(
                "SKU-OK", "Healthy", null, null, 2, "sup-2", "Beta"),
            new GoingOutOfStockReportEvaluator.ProductInput(
                "SKU-MISS", "Missing ledger", null, null, 1, "", ""),
            new GoingOutOfStockReportEvaluator.ProductInput(
                "SKU-NONE", "No threshold", null, null, null, "", ""),
        };
        var qty = new Dictionary<string, decimal>
        {
            ["SKU-CRIT"] = 1,
            ["SKU-LOW"] = 3,
            ["SKU-OK"] = 10,
        };

        var rows = GoingOutOfStockReportEvaluator.Collect(products, qty);

        Assert.Equal(["SKU-MISS", "SKU-CRIT", "SKU-LOW"], rows.Select(row => row.Sku));
        Assert.Equal(0m, rows[0].StoreQty);
        Assert.Equal("critical", rows[0].Status);
        Assert.Equal(GoingOutOfStockReportEvaluator.UnmappedVendorName, rows[0].SupplierName);
        Assert.Equal("critical", rows[1].Status);
        Assert.Equal("low", rows[2].Status);

        var criticalOnly = GoingOutOfStockReportEvaluator.Filter(rows, null, "critical");
        Assert.Equal(["SKU-MISS", "SKU-CRIT"], criticalOnly.Select(row => row.Sku));

        var totals = GoingOutOfStockReportEvaluator.Totals(rows);
        Assert.Equal(3, totals.SkuCount);
        Assert.Equal(2, totals.CriticalCount);
        Assert.Equal(1, totals.LowCount);
        Assert.Equal(1, totals.ZeroQtyCount);
    }

    [Fact]
    public void Collect_uses_default_match_qty_when_product_thresholds_missing_or_zero()
    {
        var products = new[]
        {
            new GoingOutOfStockReportEvaluator.ProductInput(
                "SKU-FALLBACK", "Uses settings", null, null, null, "sup-1", "Acme"),
            new GoingOutOfStockReportEvaluator.ProductInput(
                "SKU-ZERO-REORDER", "Zero reorder ignored", null, null, 0, "sup-1", "Acme"),
            new GoingOutOfStockReportEvaluator.ProductInput(
                "SKU-ABOVE", "Above match", null, null, null, "sup-1", "Acme"),
            new GoingOutOfStockReportEvaluator.ProductInput(
                "SKU-PRODUCT", "Product wins", null, 2, null, "sup-1", "Acme"),
        };
        var qty = new Dictionary<string, decimal>
        {
            ["SKU-FALLBACK"] = 3,
            ["SKU-ZERO-REORDER"] = 4,
            ["SKU-ABOVE"] = 6,
            ["SKU-PRODUCT"] = 2,
        };

        var rows = GoingOutOfStockReportEvaluator.Collect(products, qty, defaultMatchQty: 5m);

        Assert.Equal(["SKU-PRODUCT", "SKU-FALLBACK", "SKU-ZERO-REORDER"], rows.Select(r => r.Sku));
        Assert.Equal(5m, rows.Single(r => r.Sku == "SKU-FALLBACK").Threshold);
        Assert.Equal("critical", rows.Single(r => r.Sku == "SKU-FALLBACK").Status);
        Assert.Equal(5m, rows.Single(r => r.Sku == "SKU-ZERO-REORDER").Threshold);
        Assert.Equal(2m, rows.Single(r => r.Sku == "SKU-PRODUCT").Threshold);
        Assert.Equal("critical", rows.Single(r => r.Sku == "SKU-PRODUCT").Status);
        Assert.DoesNotContain(rows, r => r.Sku == "SKU-ABOVE");
    }

    [Fact]
    public void GetShelfThreshold_ignores_non_positive_product_fields()
    {
        var product = new GoingOutOfStockReportEvaluator.ProductInput(
            "SKU-1", "Item", 0, 0, 0, "", "");
        Assert.Null(GoingOutOfStockReportEvaluator.GetShelfThreshold(product));
        Assert.Equal(5m, GoingOutOfStockReportEvaluator.GetShelfThreshold(product, 5m));
        Assert.Equal(2m, GoingOutOfStockReportEvaluator.GetShelfThreshold(
            product with { MinStock = 2m }, 5m));
    }

    [Fact]
    public void Export_builds_going_out_of_stock_sheet()
    {
        var report = new GoingOutOfStockReportResponse
        {
            Period = new GoingOutOfStockReportPeriod
            {
                AsOf = "2026-08-14",
                StoreCode = "store-001",
                StoreName = "Main Store",
                Timezone = "Asia/Kolkata",
            },
            Totals = new GoingOutOfStockTotals
            {
                SkuCount = 1,
                CriticalCount = 1,
                ZeroQtyCount = 1,
            },
            Data =
            [
                new GoingOutOfStockRow
                {
                    Sku = "SKU-1",
                    ProductName = "Saree 1",
                    StoreQty = 0,
                    Threshold = 2,
                    Status = "critical",
                    SupplierName = "Acme",
                },
            ],
        };

        using var workbook = GoingOutOfStockReportExcelExporter.BuildWorkbook(report);
        Assert.Equal("Going Out Of Stock", Assert.Single(workbook.Worksheets).Name);
        var cells = workbook.Worksheet("Going Out Of Stock").CellsUsed().Select(cell => cell.GetString());
        Assert.Contains("Going Out Of Stock Report", cells);
        Assert.Contains("SKU-1", cells);
        Assert.Contains("critical", cells);
    }
}
