using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Billing;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class CreditBillDocumentReaderTests
{
    [Fact]
    public void ResolveStatus_pending_partial_settled()
    {
        Assert.Equal(CreditBillDocumentReader.StatusPending, CreditBillDocumentReader.ResolveStatus(1000, 0));
        Assert.Equal(CreditBillDocumentReader.StatusPartial, CreditBillDocumentReader.ResolveStatus(1000, 300));
        Assert.Equal(CreditBillDocumentReader.StatusSettled, CreditBillDocumentReader.ResolveStatus(1000, 1000));
    }

    [Fact]
    public void BuildAndRead_credit_billing_balance()
    {
        var cb = CreditBillDocumentReader.BuildCreditBillingDocument(10000, 3000, 3000, true);
        var doc = new BsonDocument { { "payable", 10000 }, { "creditBilling", cb } };

        Assert.True(CreditBillDocumentReader.IsOpen(doc));
        Assert.Equal(CreditBillDocumentReader.StatusPartial, CreditBillDocumentReader.ReadStatus(doc));
        Assert.Equal(3000m, CreditBillDocumentReader.ReadAdvanceAtPost(doc));
        Assert.Equal(7000m, CreditBillDocumentReader.ReadBalanceDue(doc));
    }

    [Fact]
    public void Settings_min_advance_math_style_defaults()
    {
        var settings = new PosBillingSettingsDocument();
        Assert.True(settings.EnableCreditBilling);
        Assert.True(settings.CreditBillingAllowZeroAdvance);
        Assert.True(settings.CreditBillingAllowPartialCollection);
        Assert.Equal(0m, settings.CreditBillingMinimumAdvancePercent);
    }
}
