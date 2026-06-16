using System;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Store;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class OnlineCodDocumentReaderTests
{
    [Fact]
    public void Pending_online_cod_bill_has_zero_cash_payments()
    {
        var doc = new BsonDocument
        {
            { "salesChannel", "online" },
            { "payable", 1500 },
            { "paymentMode", "OnlineCod" },
            { "payments", new BsonArray() },
            { "onlineCod", new BsonDocument
                {
                    { "status", "pending" },
                    { "amount", 1500 },
                }},
        };

        Assert.True(OnlineCodDocumentReader.IsOnlineCodPending(doc));
        var totals = DayBillingCloseDocumentReader.SumBillPayments(doc);
        Assert.Equal(0m, totals.Cash);
        Assert.Equal(0m, totals.Card);
        Assert.Equal(0m, totals.Upi);
    }

    [Fact]
    public void Received_online_cod_counts_in_payment_totals()
    {
        var doc = new BsonDocument
        {
            { "salesChannel", "online" },
            { "onlineCod", new BsonDocument
                {
                    { "status", "received" },
                    { "amount", 1500 },
                    { "transactionNo", "TXN-001" },
                    { "receivedPaymentMode", "UPI" },
                }},
            { "payments", new BsonArray
                {
                    new BsonDocument
                    {
                        { "provider", "Razorpay" },
                        { "amount", 1500 },
                        { "reference", "TXN-001" },
                        { "status", "posted" },
                    },
                }},
        };

        Assert.False(OnlineCodDocumentReader.IsOnlineCodPending(doc));
        var totals = DayBillingCloseDocumentReader.SumBillPayments(doc);
        Assert.Equal(1500m, totals.Upi);
    }

    [Fact]
    public void InCreatedDateRange_filters_utc_dates()
    {
        var utc = new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc);
        Assert.True(OnlineCodDocumentReader.InCreatedDateRange(utc, utc.Date, utc.Date));
        Assert.False(OnlineCodDocumentReader.InCreatedDateRange(utc, utc.Date.AddDays(1), null));
    }
}
