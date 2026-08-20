using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Store;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public sealed class SkuSalesReportTests
{
    [Fact]
    public void Collect_nets_returns_groups_unmapped_skus_and_ranks_fast_sellers()
    {
        var first = BsonDocument.Parse("""
        {
          "status":"posted","billNo":"B-1",
          "lines":[
            {"sku":"SKU-A","description":"Saree A","qty":5,"rate":100,"amount":500},
            {"sku":"SKU-C","description":"Saree C","qty":4,"rate":100,"amount":400}
          ]
        }
        """);
        var second = BsonDocument.Parse("""
        {
          "status":"posted","billNo":"B-2",
          "lines":[{"sku":"SKU-B","description":"Saree B","qty":3,"rate":90,"amount":270}]
        }
        """);
        var voided = BsonDocument.Parse("""
        {
          "status":"void","billNo":"VOID-1",
          "lines":[{"sku":"SKU-Z","qty":99,"rate":10,"amount":990}]
        }
        """);
        var postedReturn = BsonDocument.Parse("""
        {
          "status":"posted","returnNo":"R-1","originalBillNo":"B-1",
          "returnLines":[{"sku":"SKU-A","returnQty":1,"lineTotal":100}]
        }
        """);
        var voidReturn = BsonDocument.Parse("""
        {
          "status":"void","returnNo":"R-VOID","originalBillNo":"B-1",
          "returnLines":[{"sku":"SKU-C","returnQty":4,"lineTotal":400}]
        }
        """);
        var catalog = new Dictionary<string, SkuSalesReportAggregator.SkuCatalogEntry>
        {
            ["SKU-A"] = new("SKU-A", "Saree A", "sup-1", "Acme Silks"),
            ["SKU-C"] = new("SKU-C", "Saree C", "sup-1", "Acme Silks"),
        };

        var collected = SkuSalesReportAggregator.Collect(
            [first, second, voided],
            [postedReturn, voidReturn],
            catalog);

        var bySku = collected.Rows.ToDictionary(row => row.Sku);
        Assert.Equal(4m, bySku["SKU-A"].NetQty);
        Assert.Equal(400m, bySku["SKU-A"].NetAmount);
        Assert.Equal("Acme Silks", bySku["SKU-A"].SupplierName);
        Assert.Equal(SkuSalesReportAggregator.UnmappedVendorId, bySku["SKU-B"].SupplierId);
        Assert.Equal(SkuSalesReportAggregator.UnmappedVendorName, bySku["SKU-B"].SupplierName);
        Assert.False(bySku.ContainsKey("SKU-Z"));
        Assert.Equal(11m, collected.Totals.NetQty);

        var grouped = SkuSalesReportAggregator.GroupSupplierWise(collected.Rows);
        Assert.Equal("Acme Silks", grouped[0].SupplierName);
        Assert.Equal(2, grouped[0].ProductCount);
        Assert.Equal(8m, grouped[0].NetQty);
        Assert.Equal(SkuSalesReportAggregator.UnmappedVendorName, grouped[1].SupplierName);

        var ranked = SkuSalesReportAggregator.RankFastSellers(collected.Rows);
        Assert.Equal(["SKU-A", "SKU-C", "SKU-B"], ranked.Select(row => row.Sku));
        Assert.Equal(1, ranked[0].Rank);
    }

    [Fact]
    public void Export_builds_supplier_summary_product_details_and_fast_sellers_sheets()
    {
        var product = new SkuSalesRow
        {
            Rank = 1,
            Sku = "SKU-1",
            Description = "Saree 1",
            SupplierId = "sup-1",
            SupplierName = "Acme Silks",
            SoldQty = 5,
            ReturnQty = 1,
            NetQty = 4,
            SoldAmount = 500,
            ReturnAmount = 100,
            NetAmount = 400,
        };
        var supplierReport = new SupplierWiseReportResponse
        {
            Period = new SkuSalesReportPeriod
            {
                From = "2026-08-01",
                To = "2026-08-13",
                StoreCode = "store-001",
                StoreName = "Main Store",
                Timezone = "Asia/Kolkata",
            },
            Totals = new SkuSalesTotals { SupplierCount = 1, SkuCount = 1, NetQty = 4, NetAmount = 400m },
            Data =
            [
                new SupplierWiseSupplierRow
                {
                    SupplierId = "sup-1",
                    SupplierName = "Acme Silks",
                    ProductCount = 1,
                    NetQty = 4,
                    NetAmount = 400m,
                    Products = [product],
                },
            ],
        };

        using var supplierBook = SupplierWiseReportExcelExporter.BuildWorkbook(supplierReport);
        Assert.Equal(["Summary", "Product Details"], supplierBook.Worksheets.Select(sheet => sheet.Name));
        Assert.Contains("Supplier-Wise Sales Report", supplierBook.Worksheet("Summary").CellsUsed()
            .Select(cell => cell.GetString()));
        Assert.Contains("SKU-1", supplierBook.Worksheet("Product Details").CellsUsed()
            .Select(cell => cell.GetString()));

        var fastReport = new FastSellersReportResponse
        {
            Period = supplierReport.Period,
            Totals = new SkuSalesTotals { SkuCount = 1, NetQty = 4, NetAmount = 400m },
            Data = [product],
        };
        using var fastBook = FastSellersReportExcelExporter.BuildWorkbook(fastReport);
        Assert.Equal("Fast Sellers", Assert.Single(fastBook.Worksheets).Name);
        Assert.Contains("Fast Sellers Report", fastBook.Worksheet("Fast Sellers").CellsUsed()
            .Select(cell => cell.GetString()));
        Assert.Contains("SKU-1", fastBook.Worksheet("Fast Sellers").CellsUsed()
            .Select(cell => cell.GetString()));
    }
}
