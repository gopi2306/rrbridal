using System.Collections.Generic;
using System.Windows.Documents;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Invoicing;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class OutboundDispatchPrintMapperTests
{
    [Fact]
    public void FromDocument_maps_canonical_dispatch_and_bill_snapshot()
    {
        var input = OutboundDispatchPrintMapper.FromDocument(
            BuildDispatch("DSP-001", "customer", 125.50m, 2),
            Store());

        Assert.Equal("DSP-001", input.DispatchNo);
        Assert.Equal("BAT-001", input.BatchNo);
        Assert.Equal("BILL-001", input.BillNo);
        Assert.Equal("Anu", input.RecipientName);
        Assert.Contains("Market Road", input.RecipientAddress);
        Assert.Equal("Blue Dart", input.CarrierName);
        Assert.Equal("TRK-001", input.TrackingNo);
        Assert.Equal(2, input.PackageCount);
        Assert.Equal(125.50m, input.DispatchFee);
        var line = Assert.Single(input.Lines);
        Assert.Equal("SKU-RED", line.Sku);
        Assert.Equal("Red bridal saree", line.Description);
        Assert.Equal(2m, line.Quantity);
    }

    [Fact]
    public void ToBatch_calculates_parcel_and_fee_payer_totals()
    {
        var input = OutboundDispatchPrintMapper.ToBatch(
            new[]
            {
                BuildDispatch("DSP-001", "customer", 125.50m, 2),
                BuildDispatch("DSP-002", "store", 80m, 1),
            },
            Store());

        Assert.Equal(3, input.TotalParcels);
        Assert.Equal(125.50m, input.CustomerPaidFee);
        Assert.Equal(80m, input.StorePaidFee);
        Assert.Equal(2, input.Rows.Count);
    }

    [Fact]
    public void ToChargeReceipt_maps_separate_dispatch_charge()
    {
        var input = OutboundDispatchPrintMapper.ToChargeReceipt(
            BuildDispatch("DSP-001", "customer", 125.50m, 2),
            Store());

        Assert.Equal("RCPT-001", input.ReceiptNo);
        Assert.Equal("DSP-001", input.DispatchNo);
        Assert.Equal("BILL-001", input.BillNo);
        Assert.Equal(125.50m, input.Amount);
        Assert.Equal("UPI", input.PaymentMode);
        Assert.Equal("UPI-REF-1", input.Reference);
        Assert.Equal("Cashier", input.ReceivedBy);
    }

    internal static StoreProfile Store() => new()
    {
        StoreName = "RR Bridal Test",
        Address = "Main Street",
        CustomerCarePhone = "0440000000",
    };

    internal static BsonDocument BuildDispatch(
        string dispatchNo,
        string feePayer,
        decimal fee,
        int packages) => new()
    {
        { "dispatchNo", dispatchNo },
        { "batchNo", "BAT-001" },
        { "billNo", "BILL-001" },
        { "status", "Ready" },
        { "businessDate", "2026-08-12" },
        { "carrierType", "courier" },
        { "carrierName", "Blue Dart" },
        { "trackingNo", "TRK-001" },
        { "packageCount", packages },
        { "feePayer", feePayer },
        { "dispatchFee", (double)fee },
        { "shipTo", new BsonDocument
            {
                { "name", "Anu" },
                { "phone", "9876543210" },
                { "addressLine1", "12 Market Road" },
                { "addressLine2", "Near Temple" },
                { "city", "Chennai" },
                { "state", "Tamil Nadu" },
                { "pincode", "600001" },
            }
        },
        { "billSnapshot", new BsonDocument
            {
                { "billNo", "BILL-001" },
                { "customer", new BsonDocument
                    {
                        { "name", "Anu" },
                        { "phone", "9876543210" },
                    }
                },
            }
        },
        { "lines", new BsonArray
            {
                new BsonDocument
                {
                    { "sku", "SKU-RED" },
                    { "description", "Red bridal saree" },
                    { "qty", 2 },
                },
            }
        },
        { "chargeReceiptNo", "RCPT-001" },
        { "chargeReceipt", new BsonDocument
            {
                { "receiptNo", "RCPT-001" },
                { "billNo", "BILL-001" },
                { "dispatchNo", dispatchNo },
                { "customerName", "Anu" },
                { "customerPhone", "9876543210" },
                { "amount", (double)fee },
                { "mode", "UPI" },
                { "reference", "UPI-REF-1" },
                { "createdAtUtc", "2026-08-12T10:30:00Z" },
                { "receivedBy", "Cashier" },
            }
        },
        { "audit", new BsonArray
            {
                new BsonDocument { { "actorName", "Packer" } },
            }
        },
    };
}

public class OutboundDispatchPrintRenderingTests
{
    [Fact]
    public void Thermal_artifacts_include_required_identity_and_totals()
    {
        var dispatch = OutboundDispatchPrintMapper.FromDocument(
            OutboundDispatchPrintMapperTests.BuildDispatch("DSP-001", "customer", 125.50m, 2),
            OutboundDispatchPrintMapperTests.Store());
        var batch = OutboundDispatchPrintMapper.ToBatch(
            new[]
            {
                OutboundDispatchPrintMapperTests.BuildDispatch("DSP-001", "customer", 125.50m, 2),
                OutboundDispatchPrintMapperTests.BuildDispatch("DSP-002", "store", 80m, 1),
            },
            OutboundDispatchPrintMapperTests.Store());
        var receipt = OutboundDispatchPrintMapper.ToChargeReceipt(
            OutboundDispatchPrintMapperTests.BuildDispatch("DSP-001", "customer", 125.50m, 2),
            OutboundDispatchPrintMapperTests.Store());

        var parcelText = OutboundDispatchThermalTextBuilder.BuildParcelNote(dispatch);
        var batchText = OutboundDispatchThermalTextBuilder.BuildBatchSummary(batch);
        var receiptText = OutboundDispatchThermalTextBuilder.BuildChargeReceipt(receipt);

        Assert.Contains("PARCEL NOTE", parcelText);
        Assert.Contains("SKU-RED Red bridal saree", parcelText);
        Assert.Contains("Recipient sign", parcelText);
        Assert.Contains("DISPATCH BATCH SUMMARY", batchText);
        Assert.Contains("Total parcels", batchText);
        Assert.Contains("Customer-paid fees", batchText);
        Assert.Contains("Store-paid fees", batchText);
        Assert.Contains("DISPATCH CHARGE RECEIPT", receiptText);
        Assert.Contains("Separate dispatch charge", receiptText);
        Assert.Contains("RCPT-001", receiptText);
        Assert.Contains("UPI-REF-1", receiptText);
    }

    [Fact]
    public void A4_artifacts_include_basic_required_content()
    {
        var source = OutboundDispatchPrintMapperTests.BuildDispatch("DSP-001", "customer", 125.50m, 2);
        var store = OutboundDispatchPrintMapperTests.Store();
        var parcel = OutboundDispatchPrintMapper.FromDocument(source, store);
        var batch = OutboundDispatchPrintMapper.ToBatch(new[] { source }, store);
        var receipt = OutboundDispatchPrintMapper.ToChargeReceipt(source, store);

        var parcelText = DocumentText(OutboundDispatchA4DocumentBuilder.CreateParcelNote(parcel));
        var batchText = DocumentText(OutboundDispatchA4DocumentBuilder.CreateBatchSummary(batch));
        var receiptText = DocumentText(OutboundDispatchA4DocumentBuilder.CreateChargeReceipt(receipt));

        Assert.Contains("PARCEL NOTE", parcelText);
        Assert.Contains("DSP-001", parcelText);
        Assert.Contains("Red bridal saree", parcelText);
        Assert.Contains("DISPATCH BATCH SUMMARY", batchText);
        Assert.Contains("Customer-paid fee total", batchText);
        Assert.Contains("DISPATCH CHARGE RECEIPT", receiptText);
        Assert.Contains("Separate dispatch charge", receiptText);
        Assert.Contains("Dispatch charge amount", receiptText);
    }

    private static string DocumentText(FlowDocument document) =>
        new TextRange(document.ContentStart, document.ContentEnd).Text;
}
