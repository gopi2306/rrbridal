using RRBridal.StoreBilling.App.Models;
using RRBridal.StoreBilling.App.Services.Billing;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class LegacyReturnLineItemTests
{
    [Fact]
    public void Computes_inclusive_totals_from_qty_and_rate()
    {
        var line = new LegacyReturnLineItem
        {
            LineNo = 1,
            ProductCode = "SKU-1",
            Description = "Test",
            Qty = 2,
            Rate = 1180m,
            TaxPercent = 18m,
            IsIgst = false,
        };

        Assert.Equal(2360m, line.GrossReturnAmount);
        Assert.Equal(2360m, line.LineReturnTotal);
        Assert.True(line.TaxableReturnAmount > 0);
        Assert.Equal(line.CgstAmount + line.SgstAmount, line.TaxAmount);
    }
}

public class SaleReturnHistoryServiceLegacyTests
{
    [Fact]
    public void IsLegacyReturn_reads_flag()
    {
        var doc = new MongoDB.Bson.BsonDocument { { "isLegacy", true } };
        Assert.True(SaleReturnHistoryService.IsLegacyReturn(doc));
        Assert.False(SaleReturnHistoryService.IsLegacyReturn(new MongoDB.Bson.BsonDocument()));
    }
}
