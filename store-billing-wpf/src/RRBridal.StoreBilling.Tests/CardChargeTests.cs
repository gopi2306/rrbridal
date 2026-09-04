using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Invoicing;
using MongoDB.Bson;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public sealed class CardChargeTests
{
    [Fact]
    public void ComputeSuggested_applies_percent_and_flat_when_enabled()
    {
        var fee = CardChargeCalculator.ComputeSuggested(
            cardBase: 1000m,
            enabled: true,
            percent: 2m,
            flatAmount: 5m);
        Assert.Equal(25m, fee);
    }

    [Fact]
    public void ComputeSuggested_returns_zero_when_disabled_or_no_card_base()
    {
        Assert.Equal(0m, CardChargeCalculator.ComputeSuggested(1000m, enabled: false, 2m, 5m));
        Assert.Equal(0m, CardChargeCalculator.ComputeSuggested(0m, enabled: true, 2m, 5m));
        Assert.Equal(0m, CardChargeCalculator.ComputeSuggested(-1m, enabled: true, 2m, 5m));
    }

    [Fact]
    public void ComputeSuggested_percent_only_and_flat_only()
    {
        Assert.Equal(20m, CardChargeCalculator.ComputeSuggested(1000m, true, 2m, 0m));
        Assert.Equal(5m, CardChargeCalculator.ComputeSuggested(1000m, true, 0m, 5m));
    }

    [Fact]
    public void Thermal_mapper_and_builder_include_card_charges_line()
    {
        var doc = new BsonDocument
        {
            { "billNo", "B-1" },
            { "billDate", "31-AUG-2026" },
            { "posCounter", "1" },
            { "subTotal", 1000 },
            { "roundOff", 0 },
            { "cardCharge", 25 },
            { "payable", 1025 },
            { "lines", new BsonArray
                {
                    new BsonDocument
                    {
                        { "lineNo", 1 },
                        { "description", "Saree" },
                        { "qty", 1 },
                        { "rate", 1000 },
                        { "amount", 1000 },
                        { "revisedAmount", 1000 },
                        { "revisedTaxAmount", 0 },
                        { "revisedInclusiveAmount", 1000 },
                    },
                }
            },
        };

        var input = BillThermalMapper.MapFromBillDocument(
            doc,
            new StoreProfile { StoreName = "RR Bridal", Address = "Chennai" },
            charWidth: 48,
            isDuplicate: false);

        Assert.Equal(25m, input.CardCharge);
        Assert.Equal(1025m, input.Payable);

        var text = ThermalInvoiceTextBuilder.Build(input);
        Assert.Contains("Card charges: 25.00", text);
        Assert.Contains("Bill Amount .: 1025.00", text);
        Assert.DoesNotContain("Other charges:", text);
    }
}
