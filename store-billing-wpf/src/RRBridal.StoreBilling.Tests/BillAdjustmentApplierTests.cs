using System;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Billing;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class BillAdjustmentApplierTests
{
    [Fact]
    public void ApplyToBill_updates_line_rates_payable_and_adjustment_markers()
    {
        var bill = BsonDocument.Parse(
            """
            {
              "billNo": "20260930-001-1-0422",
              "payable": 788.0,
              "subTotal": 750.0,
              "cgstTotal": 18.75,
              "sgstTotal": 18.75,
              "igstTotal": 0.0,
              "taxTotal": 37.5,
              "roundOff": 0.5,
              "lines": [
                {
                  "lineNo": 1,
                  "sku": "SKU-00031",
                  "qty": 1,
                  "rate": 750.0,
                  "amount": 750.0,
                  "taxPercent": 5.0,
                  "cgstPercent": 2.5,
                  "sgstPercent": 2.5,
                  "igstPercent": 0.0,
                  "cgstAmount": 18.75,
                  "sgstAmount": 18.75,
                  "igstAmount": 0.0,
                  "taxAmount": 37.5
                }
              ]
            }
            """);

        var adjustedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        BillAdjustmentApplier.ApplyToBill(
            bill,
            [
                new BillAdjustmentApplier.AdjustmentLineSnap(1, 1m, 100m, 100m, 5m),
            ],
            isInterState: false,
            adjustmentNo: "ADJ-20261001-001-1-0847",
            adjustedPayable: 105m,
            adjustedAt);

        var line = bill["lines"].AsBsonArray[0].AsBsonDocument;
        Assert.Equal(100.0, line["rate"].ToDouble());
        Assert.Equal(100.0, line["amount"].ToDouble());
        Assert.Equal(2.5, line["cgstAmount"].ToDouble());
        Assert.Equal(2.5, line["sgstAmount"].ToDouble());
        Assert.Equal(105.0, bill["payable"].ToDouble());
        Assert.Equal(100.0, bill["subTotal"].ToDouble());
        Assert.True(bill["hasAdjustment"].AsBoolean);
        Assert.Equal("ADJ-20261001-001-1-0847", bill["lastAdjustmentNo"].AsString);
    }

    [Fact]
    public void FromAdjustmentLinesArray_maps_payload_lines()
    {
        var lines = new BsonArray
        {
            new BsonDocument
            {
                { "lineNo", 1 },
                { "adjustedQty", 2.0 },
                { "adjustedRate", 50.0 },
                { "adjustedAmount", 100.0 },
                { "taxPercent", 12.0 },
            },
        };

        var snaps = BillAdjustmentApplier.FromAdjustmentLinesArray(lines);
        Assert.Single(snaps);
        Assert.Equal(1, snaps[0].LineNo);
        Assert.Equal(2m, snaps[0].AdjustedQty);
        Assert.Equal(50m, snaps[0].AdjustedRate);
        Assert.Equal(100m, snaps[0].AdjustedAmount);
        Assert.Equal(12m, snaps[0].TaxPercent);
    }
}
