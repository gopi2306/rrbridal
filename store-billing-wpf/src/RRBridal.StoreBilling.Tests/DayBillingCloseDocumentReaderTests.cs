using System;
using System.Collections.Generic;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Store;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class DayBillingCloseDocumentReaderTests
{
    [Fact]
    public void MatchesLocalDay_uses_local_timezone()
    {
        var localDate = new DateTime(2026, 6, 4);
        var utcEvening = new DateTime(2026, 6, 4, 18, 30, 0, DateTimeKind.Utc);
        var doc = new BsonDocument
        {
            { "createdAtUtc", utcEvening.ToString("O") },
        };

        var localDay = utcEvening.ToLocalTime().Date;
        Assert.True(DayBillingCloseDocumentReader.MatchesLocalDay(doc, localDay));
        Assert.False(DayBillingCloseDocumentReader.MatchesLocalDay(doc, localDay.AddDays(1)));
    }

    [Fact]
    public void SumBillPayments_aggregates_split_payments()
    {
        var doc = BsonDocument.Parse(
            """
            {
              "payments": [
                { "provider": "Cash", "amount": 1000 },
                { "provider": "PineLabs", "amount": 500 },
                { "provider": "Razorpay", "amount": 250 },
                { "provider": "CreditNote", "amount": 100 }
              ]
            }
            """);

        var totals = DayBillingCloseDocumentReader.SumBillPayments(doc);

        Assert.Equal(1000m, totals.Cash);
        Assert.Equal(500m, totals.Card);
        Assert.Equal(250m, totals.Upi);
        Assert.Equal(100m, totals.CreditNote);
    }

    [Fact]
    public void SumBillLineQty_skips_zero_amount_lines()
    {
        var doc = BsonDocument.Parse(
            """
            {
              "lines": [
                { "qty": 2, "amount": 100, "revisedAmount": 95 },
                { "qty": 5, "amount": 0, "revisedAmount": 0 }
              ]
            }
            """);

        Assert.Equal(2m, DayBillingCloseDocumentReader.SumBillLineQty(doc));
    }

    [Fact]
    public void ResolveSyncStatus_maps_outbox_states()
    {
        var bill = new BsonDocument { { "billNo", "B-001" } };
        var synced = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["B-001"] = "synced",
        };
        var pending = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["B-001"] = "pending",
        };

        Assert.Equal("Synced", DayBillingCloseDocumentReader.ResolveSyncStatus(bill, synced));
        Assert.Equal("Pending sync", DayBillingCloseDocumentReader.ResolveSyncStatus(bill, pending));
    }

    [Fact]
    public void ResolveSyncStatus_not_queued_when_postWarnings_mention_outbox()
    {
        var bill = BsonDocument.Parse(
            """
            {
              "billNo": "B-002",
              "postWarnings": ["Outbox enqueue failed: timeout"]
            }
            """);

        Assert.Equal("Not queued", DayBillingCloseDocumentReader.ResolveSyncStatus(bill, new Dictionary<string, string>()));
    }

    [Fact]
    public void BuildOutboxSyncByBillNo_prefers_synced_over_pending()
    {
        var events = new[]
        {
            BsonDocument.Parse("""{ "type": "InvoiceCreated", "status": "pending", "payload": { "billNo": "X-1" } }"""),
            BsonDocument.Parse("""{ "type": "InvoiceCreated", "status": "synced", "payload": { "billNo": "X-1" } }"""),
        };

        var map = DayBillingCloseDocumentReader.BuildOutboxSyncByBillNo(events);

        Assert.Equal("synced", map["X-1"]);
    }

    [Fact]
    public void IsPostedBill_treats_missing_status_as_posted()
    {
        Assert.True(DayBillingCloseDocumentReader.IsPostedBill(new BsonDocument()));
        Assert.False(DayBillingCloseDocumentReader.IsPostedBill(new BsonDocument { { "status", "draft" } }));
    }
}
