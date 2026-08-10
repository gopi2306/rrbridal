using ClosedXML.Excel;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Inventory;
using RRBridal.StoreBilling.App.Services.Sync;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public sealed class PhysicalInventoryImportTests
{
    [Fact]
    public void Template_has_required_headers_and_blank_physical_quantity()
    {
        using var workbook = PhysicalInventoryExcelService.BuildTemplateWorkbook(
        [
            new InventoryGridRow { Sku = "SKU-1", Product = "Product One", StoreQty = 3 },
        ]);
        var sheet = workbook.Worksheet("Physical Inventory");

        Assert.Equal("SKU", sheet.Cell(1, 1).GetString());
        Assert.Equal("Item Name", sheet.Cell(1, 2).GetString());
        Assert.Equal("Phy Qty", sheet.Cell(1, 3).GetString());
        Assert.Equal("SKU-1", sheet.Cell(2, 1).GetString());
        Assert.True(sheet.Cell(2, 3).IsEmpty());
    }

    [Fact]
    public void Parser_skips_blank_counts_and_calculates_set_to_variance()
    {
        var path = CreateWorkbook(
            ["SKU", "Item Name", "Phy Qty"],
            ["SKU-1", "Product One", 5],
            ["SKU-2", "Product Two", ""]);
        try
        {
            var preview = PhysicalInventoryExcelService.ParseWorkbook(
                path,
                [
                    new InventoryGridRow { Sku = "SKU-1", Product = "Product One", StoreQty = 3 },
                    new InventoryGridRow { Sku = "SKU-2", Product = "Product Two", StoreQty = 9 },
                ]);

            var line = Assert.Single(preview.Lines);
            Assert.Equal(5m, line.NewQty);
            Assert.Equal(2m, line.QtyDelta);
            Assert.True(preview.CanCommit);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parser_blocks_duplicates_negative_counts_and_unknown_skus()
    {
        var path = CreateWorkbook(
            ["SKU", "Item Name", "Phy Qty"],
            ["SKU-1", "Product One", 5],
            ["SKU-1", "Product One", 6],
            ["SKU-2", "Product Two", -1],
            ["SKU-X", "Unknown", 2]);
        try
        {
            var preview = PhysicalInventoryExcelService.ParseWorkbook(
                path,
                [new InventoryGridRow { Sku = "SKU-1", Product = "Product One", StoreQty = 3 }]);

            Assert.Equal(3, preview.Errors.Count);
            Assert.False(preview.CanCommit);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Bulk_publisher_preserves_deterministic_import_event_id()
    {
        var context = new StoreContext();
        var db = new MongoClient(
                "mongodb://127.0.0.1:27099/?serverSelectionTimeoutMS=10&connectTimeoutMS=10")
            .GetDatabase("physical_import_test");
        var publisher = new BillingOutboxPublisher(db, context);
        string? dispatchedEventId = null;
        BsonDocument? dispatchedPayload = null;
        publisher.ConfigureOnlineDispatch(
            () => true,
            _ => Task.CompletedTask,
            (_, payload, _, eventId, _) =>
            {
                dispatchedEventId = eventId;
                dispatchedPayload = payload;
                return Task.CompletedTask;
            });

        var eventId = await publisher.PublishPhysicalInventoryImportAsync(
            "IADJ-1",
            "Physical count",
            [("SKU-1", 5m), ("SKU-2", 0m)],
            "physical-inventory-import:store-001:abc");

        Assert.Equal(eventId, dispatchedEventId);
        Assert.Equal(2, dispatchedPayload!["lines"].AsBsonArray.Count);
        Assert.Equal(5d, dispatchedPayload["lines"][0]["newQty"].AsDouble);
    }

    private static string CreateWorkbook(string[] headers, params object[][] rows)
    {
        var path = Path.Combine(Path.GetTempPath(), $"physical-inventory-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Physical Inventory");
        for (var column = 0; column < headers.Length; column++)
            sheet.Cell(1, column + 1).Value = headers[column];
        for (var row = 0; row < rows.Length; row++)
        for (var column = 0; column < rows[row].Length; column++)
            sheet.Cell(row + 2, column + 1).Value = XLCellValue.FromObject(rows[row][column]);
        workbook.SaveAs(path);
        return path;
    }
}
