using System;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Dispatch;
using RRBridal.StoreBilling.App.Services.Sync;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public sealed class OutboundDispatchDomainTests
{
    [Theory]
    [InlineData(OutboundDispatchStatus.Draft, OutboundDispatchStatus.Ready, true)]
    [InlineData(OutboundDispatchStatus.Draft, OutboundDispatchStatus.HandedOver, false)]
    [InlineData(OutboundDispatchStatus.Ready, OutboundDispatchStatus.HandedOver, true)]
    [InlineData(OutboundDispatchStatus.Ready, OutboundDispatchStatus.Draft, false)]
    [InlineData(OutboundDispatchStatus.Ready, OutboundDispatchStatus.Delivered, false)]
    [InlineData(OutboundDispatchStatus.HandedOver, OutboundDispatchStatus.Delivered, true)]
    [InlineData(OutboundDispatchStatus.Delivered, OutboundDispatchStatus.Returned, true)]
    [InlineData(OutboundDispatchStatus.Cancelled, OutboundDispatchStatus.Draft, false)]
    public void Status_transitions_are_strict(string from, string to, bool expected)
    {
        Assert.Equal(expected, OutboundDispatchStatus.CanTransition(from, to));
    }

    [Fact]
    public void Address_resolver_prefers_customer_master_and_honours_manual_override()
    {
        var bill = BsonDocument.Parse("""
            { "customerName": "Bill Name", "customerPhone": "111", "customerAddress": "Bill street" }
            """);
        var customer = BsonDocument.Parse("""
            {
              "name": "Master Name", "phone": "222", "street": "Master street",
              "place": "Second line", "city": "Chennai", "state": "Tamil Nadu", "pincode": "600001"
            }
            """);

        var resolved = DispatchAddressResolver.Resolve(bill, customer);
        Assert.Equal("Master street", resolved.AddressLine1);
        Assert.Equal("customer_master", resolved.Source);

        var manual = new DispatchAddress("Override", "333", "Manual street", "", "Madurai", "Tamil Nadu", "625001");
        resolved = DispatchAddressResolver.Resolve(bill, customer, manual);
        Assert.Equal("Manual street", resolved.AddressLine1);
        Assert.Equal("manual", resolved.Source);
    }

    [Fact]
    public void Mapper_captures_bill_customer_and_line_snapshots_without_stock_mutation()
    {
        var bill = BsonDocument.Parse("""
            {
              "billNo": "B-1", "status": "posted", "billDate": "2026-08-12",
              "customerCode": "C-1", "customerName": "Customer", "customerPhone": "999",
              "payable": 1500,
              "lines": [{ "lineNo": 1, "sku": "SKU-1", "description": "Dress", "qty": 1, "amount": 1500 }]
            }
            """);
        var draft = new OutboundDispatchDraft
        {
            ShipTo = new DispatchAddress("Customer", "999", "Street", "", "Chennai", "Tamil Nadu", "600001"),
            CarrierType = DispatchCarrierType.Courier,
            CarrierName = "Carrier",
            TrackingNo = "TRACK-1",
            PackageCount = 2,
            FeePayer = DispatchFeePayer.Customer,
            Fee = 100m,
        };

        var doc = OutboundDispatchDocumentMapper.Build(
            "DSP-1", "DBAT-1", "S-1", "D-1", "1", bill, draft, "2026-08-12T10:00:00Z");

        Assert.Equal(OutboundDispatchStatus.Draft, doc["status"].AsString);
        Assert.True(doc["active"].AsBoolean);
        Assert.Equal("C-1", doc["billSnapshot"]["customer"]["customerCode"].AsString);
        Assert.Equal("SKU-1", doc["lines"][0]["sku"].AsString);
        Assert.Equal("courier", doc["carrierType"].AsString);
        Assert.Equal("Carrier", doc["carrierName"].AsString);
        Assert.Equal("TRACK-1", doc["trackingNo"].AsString);
        Assert.Equal("customer", doc["feePayer"].AsString);
        Assert.Equal(100d, doc["dispatchFee"].AsDouble);
        Assert.False(doc.Contains("carrier"));
        Assert.False(doc.Contains("fee"));
        Assert.False(doc["billSnapshot"].AsBsonDocument.Contains("lines"));
        Assert.False(doc.Contains("stockDelta"));
        Assert.False(doc["billSnapshot"].AsBsonDocument.Contains("stockDelta"));
    }

    [Fact]
    public void Mapper_rejects_non_posted_bill_and_invalid_carrier()
    {
        var bill = BsonDocument.Parse("""{ "billNo": "B-1", "status": "void" }""");
        var draft = new OutboundDispatchDraft
        {
            ShipTo = new DispatchAddress("Customer", "", "Street", "", "", "", ""),
            CarrierType = "truck",
        };

        Assert.Throws<InvalidOperationException>(() => OutboundDispatchDocumentMapper.Build(
            "DSP-1", "DBAT-1", "S-1", "D-1", "1", bill, draft, "2026-08-12T10:00:00Z"));
    }

    [Fact]
    public void Canonical_mapper_flattens_legacy_local_shape()
    {
        var legacy = BsonDocument.Parse("""
            {
              "dispatchNo": "DSP-1", "billNo": "B-1",
              "billSnapshot": { "billNo": "B-1", "lines": [{ "sku": "SKU-1" }] },
              "carrier": { "type": "post", "name": "India Post", "trackingNo": "P-1" },
              "fee": { "payer": "store", "amount": 80, "paymentMode": "Cash" },
              "linkedExpense": { "expenseNo": "EXP-1", "status": "posted" }
            }
            """);

        var payload = OutboundDispatchDocumentMapper.ToCanonicalPayload(legacy);

        Assert.Equal("post", payload["carrierType"].AsString);
        Assert.Equal("India Post", payload["carrierName"].AsString);
        Assert.Equal("store", payload["feePayer"].AsString);
        Assert.Equal(80m, OutboundDispatchDocumentMapper.ReadDecimal(payload, "dispatchFee"));
        Assert.Equal("SKU-1", payload["lines"][0]["sku"].AsString);
        Assert.Equal("EXP-1", payload["expenseNo"].AsString);
        Assert.False(payload.Contains("carrier"));
        Assert.False(payload.Contains("fee"));
        Assert.False(payload.Contains("linkedExpense"));
    }

    [Fact]
    public void Active_semantics_exclude_all_terminal_statuses()
    {
        Assert.True(OutboundDispatchStatus.IsActive(OutboundDispatchStatus.Draft));
        Assert.True(OutboundDispatchStatus.IsActive(OutboundDispatchStatus.Ready));
        Assert.True(OutboundDispatchStatus.IsActive(OutboundDispatchStatus.HandedOver));
        Assert.False(OutboundDispatchStatus.IsActive(OutboundDispatchStatus.Delivered));
        Assert.False(OutboundDispatchStatus.IsActive(OutboundDispatchStatus.Cancelled));
        Assert.False(OutboundDispatchStatus.IsActive(OutboundDispatchStatus.Returned));
    }

    [Fact]
    public void Event_names_match_central_sync_contract()
    {
        Assert.Equal("OutboundDispatchCreated", OutboundDispatchEventType.Created);
        Assert.Equal("OutboundDispatchUpdated", OutboundDispatchEventType.Updated);
        Assert.Equal("OutboundDispatchStatusChanged", OutboundDispatchEventType.StatusChanged);
        Assert.Equal("OutboundDispatchChargeReceived", OutboundDispatchEventType.ChargeReceived);
        Assert.Equal("OutboundDispatchCancelled", OutboundDispatchEventType.Cancelled);
    }
}
