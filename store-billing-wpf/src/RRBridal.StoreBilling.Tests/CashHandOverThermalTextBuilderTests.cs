using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Store;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class CashHandOverThermalTextBuilderTests
{
    [Fact]
    public void Build_includes_denomination_grid_and_totals()
    {
        var input = new CashHandOverThermalInput
        {
            Store = new StoreProfile { StoreName = "RR Bridal Test" },
            CharWidth = 48,
            BusinessDate = "09/06/2026",
            Counter = "4",
            UserName = "SFAISAL",
            Denominations = new[]
            {
                new CashDenominationLine { Denomination = 500, UnitCount = 84 },
                new CashDenominationLine { Denomination = 100, UnitCount = 11 },
            },
            CashInHand = 43100m,
            MorningCash = 5000m,
            ExpectedCash = 43100m,
            Difference = 0m,
            StatusLabel = "BALANCED",
        };

        var text = CashHandOverThermalTextBuilder.Build(input);

        Assert.Contains("CASH HAND OVER", text);
        Assert.Contains("500", text);
        Assert.Contains("42,000.00", text);
        Assert.Contains("Cash In Hand:", text);
        Assert.Contains("BALANCED", text);
    }
}
