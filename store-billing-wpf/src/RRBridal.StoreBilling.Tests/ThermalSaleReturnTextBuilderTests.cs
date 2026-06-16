using System;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Invoicing;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class ThermalSaleReturnTextBuilderTests
{
    [Fact]
    public void Build_includes_duplicate_watermark_when_flag_set()
    {
        var input = new ThermalSaleReturnInput
        {
            Store = new StoreProfile { StoreName = "RR Bridal Test" },
            CharWidth = 48,
            ReturnNo = "RET-001",
            OriginalBillNo = "BILL-001",
            UserName = "Admin",
            Counter = "POS1",
            ReturnDate = "17/06/26",
            ReturnTime = "10:30AM",
            ReturnModeLabel = "Credit Note",
            CreditNoteNo = "CN-RET-001",
            IsDuplicateCopy = true,
        };

        var text = ThermalSaleReturnTextBuilder.Build(input);

        Assert.Contains("*** DUPLICATE ***", text);
        Assert.Contains("Credit Note:", text);
    }

    [Fact]
    public void Build_omits_duplicate_watermark_by_default()
    {
        var input = new ThermalSaleReturnInput
        {
            Store = new StoreProfile { StoreName = "RR Bridal Test" },
            CharWidth = 48,
            ReturnNo = "RET-001",
            OriginalBillNo = "BILL-001",
            UserName = "Admin",
            Counter = "POS1",
            ReturnDate = "17/06/26",
            ReturnTime = "10:30AM",
            ReturnModeLabel = "Cash",
        };

        var text = ThermalSaleReturnTextBuilder.Build(input);

        Assert.DoesNotContain("*** DUPLICATE ***", text);
    }
}

public class CustomerCreditNoteServiceFilterTests
{
    [Fact]
    public void InCreatedDateRange_respects_from_and_to()
    {
        var utc = new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc);

        Assert.True(CustomerCreditNoteService.InCreatedDateRange(utc, utc.Date, utc.Date));
        Assert.False(CustomerCreditNoteService.InCreatedDateRange(utc, utc.Date.AddDays(1), null));
        Assert.False(CustomerCreditNoteService.InCreatedDateRange(utc, null, utc.Date.AddDays(-1)));
    }

    [Fact]
    public void MatchesMobileFilter_normalizes_phone_prefix()
    {
        var doc = new BsonDocument
        {
            { "customerPhoneNorm", "9876543210" },
            { "customerPhone", "9876543210" },
        };

        Assert.True(CustomerCreditNoteService.MatchesMobileFilter(doc, "9876543210"));
        Assert.True(CustomerCreditNoteService.MatchesMobileFilter(doc, "+91 98765 43210"));
        Assert.False(CustomerCreditNoteService.MatchesMobileFilter(doc, "9123456789"));
        Assert.True(CustomerCreditNoteService.MatchesMobileFilter(doc, null));
    }
}
