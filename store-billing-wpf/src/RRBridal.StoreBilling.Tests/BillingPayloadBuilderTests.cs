using MongoDB.Bson;
using RRBridal.StoreBilling.App.Models;
using RRBridal.StoreBilling.App.Services.Billing;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class BillingPayloadBuilderTests
{
    [Fact]
    public void Lines_round_trip_preserves_sku_qty_rate()
    {
        var lines = new[]
        {
            new BillingLineItem
            {
                LineNo = 1,
                ProductCode = "SKU-1",
                Description = "Saree",
                Qty = 2,
                Rate = 1500,
                Amount = 3000,
                TaxPercent = 5,
            },
        };

        var arr = BillingPayloadBuilder.BuildLinesBsonArray(lines);
        var doc = new BsonDocument { { "lines", arr } };
        var loaded = BillingPayloadBuilder.LoadLinesFromDocument(doc, isInterState: false);

        Assert.Single(loaded);
        Assert.Equal("SKU-1", loaded[0].ProductCode);
        Assert.Equal(2m, loaded[0].Qty);
        Assert.Equal(1500m, loaded[0].Rate);
        Assert.Equal("Saree", loaded[0].Description);
    }

    [Fact]
    public void ReadHeader_reads_customer_and_flags()
    {
        var doc = new BsonDocument
        {
            { "billDate", "14-Jul-2026" },
            { "customerCode", "C001" },
            { "customerName", "Priya" },
            { "customerPhone", "9876543210" },
            { "salesman", "Ravi" },
            { "salesmanCode", "S01" },
            { "holdBills", false },
            { "doorDelivery", true },
            { "stitching", true },
            { "deliveryDate", "20-JUL-2026" },
            { "printInvoice", true },
            { "isInterState", false },
            { "itemDiscountPercent", 5 },
            { "cashDiscAmount", 100 },
            { "alterationGstIncluded", true },
            { "payable", 4500.5 },
        };

        var header = BillingPayloadBuilder.ReadHeader(doc);
        Assert.Equal("Priya", header.CustomerName);
        Assert.Equal("9876543210", header.CustomerPhone);
        Assert.True(header.DoorDelivery);
        Assert.True(header.Stitching);
        Assert.Equal(5m, header.ItemDiscountPercent);
        Assert.Equal(100m, header.CashDiscAmount);
        Assert.Equal(4500.5m, header.Payable);
    }

    [Fact]
    public void DeliveryDate_format_parse_round_trip()
    {
        var dt = new System.DateTime(2026, 7, 20);
        var formatted = BillingPayloadBuilder.FormatDeliveryDate(dt);
        var parsed = BillingPayloadBuilder.ParseDeliveryDate(formatted);
        Assert.Equal(dt.Date, parsed?.Date);
    }
}
