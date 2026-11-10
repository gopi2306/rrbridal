using RRBridal.StoreBilling.App.Services.Api;
using RRBridal.StoreBilling.App.Services.Billing;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public sealed class HeldBillResumeTests
{
    [Fact]
    public void BillDateForResume_uses_resume_day_not_hold_day()
    {
        var resumeDay = new DateTime(2026, 9, 12, 9, 15, 0);
        Assert.Equal("12-SEP-2026", HeldBillResume.BillDateForResume(resumeDay));
        Assert.NotEqual("11-SEP-2026", HeldBillResume.BillDateForResume(resumeDay));
    }

    [Fact]
    public void BillDateForResume_matches_assign_new_bill_identity_format()
    {
        var now = new DateTime(2026, 1, 5, 23, 59, 0);
        Assert.Equal("05-JAN-2026", HeldBillResume.BillDateForResume(now));
    }

    [Fact]
    public void HeldBillsListUrl_omits_deviceId_on_primary_counter()
    {
        var url = CentralStorePosClient.HeldBillsListUrl("store-001", "pos2-device", isPrimaryCounter: true);
        Assert.Equal("/api/store-pos/held-bills?storeCode=store-001", url);
        Assert.DoesNotContain("deviceId", url);
    }

    [Fact]
    public void HeldBillsListUrl_filters_deviceId_on_other_counters()
    {
        var url = CentralStorePosClient.HeldBillsListUrl("store-001", "pos2-device", isPrimaryCounter: false);
        Assert.Equal("/api/store-pos/held-bills?storeCode=store-001&deviceId=pos2-device", url);
    }
}
