using System;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Invoicing;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class CreditReceiptMapperTests
{
    [Fact]
    public void FromPostedBill_maps_advance_and_balance()
    {
        var bill = BuildCreditBill(
            advance: 1000m,
            paid: 1000m,
            payments: new BsonArray
            {
                new BsonDocument
                {
                    { "kind", "advance" },
                    { "receivedAtUtc", "2026-07-15T10:00:00Z" },
                    { "amount", 1000 },
                    { "mode", "Cash" },
                    { "reference", "ADV" },
                    { "receivedBy", "Admin" },
                    { "receiptNo", "" },
                },
            });

        var input = CreditReceiptMapper.FromPostedBill(
            bill,
            new StoreProfile { StoreName = "RR Bridal" },
            charWidth: 48,
            receivedBy: "Admin");

        Assert.Equal(CreditReceiptKind.CreditInvoiceAtPost, input.Kind);
        Assert.Equal(4149m, input.TotalPayable);
        Assert.Equal(1000m, input.AdvanceAtPost);
        Assert.Equal(1000m, input.AmountPaidThisTime);
        Assert.Equal(1000m, input.CumulativeAmountPaid);
        Assert.Equal(3149m, input.BalanceDue);
        Assert.Equal(CreditBillDocumentReader.StatusPartial, input.Status);
        Assert.Single(input.PaymentHistory);
        Assert.Equal("Cash", input.PaymentMode);
        Assert.Contains(input.Lines, l => l.Amount == 4149m);
    }

    [Fact]
    public void FromCollection_maps_receipt_and_history()
    {
        var bill = BuildCreditBill(
            advance: 1000m,
            paid: 1500m,
            payments: new BsonArray
            {
                new BsonDocument
                {
                    { "kind", "advance" },
                    { "receivedAtUtc", "2026-07-15T10:00:00Z" },
                    { "amount", 1000 },
                    { "mode", "Cash" },
                    { "reference", "ADV" },
                    { "receivedBy", "Admin" },
                    { "receiptNo", "" },
                },
                new BsonDocument
                {
                    { "kind", "partial" },
                    { "receivedAtUtc", "2026-07-15T12:00:00Z" },
                    { "amount", 500 },
                    { "mode", "UPI" },
                    { "reference", "UPI-1" },
                    { "receivedBy", "Admin" },
                    { "receiptNo", "RCPT-001" },
                },
            });

        var receipt = new BsonDocument
        {
            { "receiptNo", "RCPT-001" },
            { "billNo", "BILL-1" },
            { "amount", 500 },
            { "mode", "UPI" },
            { "reference", "UPI-1" },
            { "balanceDue", 2649 },
            { "amountPaid", 1500 },
            { "receivedBy", "Admin" },
            { "createdAtUtc", "2026-07-15T12:00:00Z" },
        };

        var input = CreditReceiptMapper.FromCollection(
            bill,
            receipt,
            new StoreProfile { StoreName = "RR Bridal" },
            charWidth: 48);

        Assert.Equal(CreditReceiptKind.BalanceCollection, input.Kind);
        Assert.Equal("RCPT-001", input.ReceiptNo);
        Assert.Equal(500m, input.AmountPaidThisTime);
        Assert.Equal(1500m, input.CumulativeAmountPaid);
        Assert.Equal(2649m, input.BalanceDue);
        Assert.Equal(2, input.PaymentHistory.Count);
        Assert.Equal("UPI", input.PaymentMode);
    }

    private static BsonDocument BuildCreditBill(decimal advance, decimal paid, BsonArray payments)
    {
        var total = 4149m;
        return new BsonDocument
        {
            { "billNo", "BILL-1" },
            { "billDate", "15-Jul-2026" },
            { "customerName", "Test" },
            { "customerPhone", "9999999999" },
            { "customerCode", "CUST-1" },
            { "posCounter", "1" },
            { "payable", (double)total },
            {
                "lines", new BsonArray
                {
                    new BsonDocument
                    {
                        { "lineNo", 1 },
                        { "description", "Sample item" },
                        { "qty", 1 },
                        { "rate", (double)total },
                        { "amount", (double)total },
                        { "revisedInclusiveAmount", (double)total },
                    },
                }
            },
            {
                "creditBilling", CreditBillDocumentReader.BuildCreditBillingDocument(
                    total, advance, paid, creditCustomer: true, payments)
            },
        };
    }
}

public class CreditThermalTextBuilderTests
{
    [Fact]
    public void Build_includes_totals_balance_and_history()
    {
        var input = CreditReceiptMapper.CreateSample(
            new StoreProfile { StoreName = "RR Bridal Test", ThankYouLine = "Thanks" },
            CreditPrintFormat.Thermal);

        var text = CreditThermalTextBuilder.Build(input);

        Assert.Contains("PAYMENT RECEIPT", text);
        Assert.Contains("Balance due", text);
        Assert.Contains("PAYMENT HISTORY", text);
        Assert.Contains("RR Bridal Test", text);
        Assert.Contains("Total", text);
    }
}
