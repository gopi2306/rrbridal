using RRBridal.StoreBilling.App.Services.Billing;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public sealed class AlterationBillMathTests
{
    [Fact]
    public void ExclusiveMode_AddsFlatToGrandBeforeRound()
    {
        var revisedSub = 1000m;
        var cgst = 60m;
        var sgst = 60m;
        var igst = 0m;
        var grand = 1120m;
        var lines = new List<AlterationBillMath.AlterationLine>
        {
            new(400m, 12m, false),
        };

        AlterationBillMath.Apply(false, lines, ref revisedSub, ref cgst, ref sgst, ref igst, ref grand);

        Assert.Equal(1000m, revisedSub);
        Assert.Equal(60m, cgst);
        Assert.Equal(1520m, grand);
        Assert.Equal(400m, AlterationBillMath.SumAlteration(lines));
    }

    [Fact]
    public void InclusiveMode_SplitsAlterationIntoTaxAndGrand()
    {
        var revisedSub = 1000m;
        var cgst = 60m;
        var sgst = 60m;
        var igst = 0m;
        var grand = 1120m;
        var lines = new List<AlterationBillMath.AlterationLine>
        {
            new(400m, 12m, false),
        };

        AlterationBillMath.Apply(true, lines, ref revisedSub, ref cgst, ref sgst, ref igst, ref grand);

        Assert.True(revisedSub > 1000m);
        Assert.True(cgst > 60m);
        Assert.True(sgst > 60m);
        Assert.Equal(1520m, grand);
    }

    [Fact]
    public void PayableWithRoundOff_IncludesExclusiveAlteration()
    {
        var grand = 1120m;
        var alteration = 400m;
        var combined = grand + alteration;
        var roundOff = Math.Round(combined, 0, MidpointRounding.AwayFromZero) - combined;
        var payable = combined + roundOff;
        Assert.Equal(1520m, payable);
    }
}
