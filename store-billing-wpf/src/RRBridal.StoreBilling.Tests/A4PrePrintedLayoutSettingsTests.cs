using RRBridal.StoreBilling.App.Services.Invoicing;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public sealed class A4PrePrintedLayoutSettingsTests
{
    [Fact]
    public void EnsureAlignmentDefaults_FixesLegacySameRowMetaLayout()
    {
        var settings = new A4PrePrintedLayoutSettings
        {
            InvNoTopMm = 62,
            InvNoLeftMm = 118,
            InvNoWidthMm = 35,
            DateTopMm = 62,
            DateLeftMm = 155,
            DateWidthMm = 35,
        };

        settings.EnsureAlignmentDefaults();

        Assert.Equal(57, settings.InvNoTopMm);
        Assert.Equal(140, settings.InvNoLeftMm);
        Assert.Equal(65, settings.DateTopMm);
        Assert.Equal(140, settings.DateLeftMm);
        Assert.Equal(73, settings.OrderNoTopMm);
        Assert.Equal(140, settings.OrderNoLeftMm);
    }
}
