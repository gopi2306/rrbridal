using Xunit;
using RRBridal.StoreBilling.App.Services.Invoicing;

namespace RRBridal.StoreBilling.Tests;

public sealed class IndianAmountInWordsTests
{
    [Fact]
    public void ForRupee_SampleInvoiceTotal()
    {
        Assert.Equal("INR Seven Thousand Two Hundred Seventy Only", IndianAmountInWords.ForRupee(7270m));
    }

    [Fact]
    public void ForRupee_Zero()
    {
        Assert.Equal("INR Zero Only", IndianAmountInWords.ForRupee(0m));
    }

    [Fact]
    public void ForRupee_WithPaise()
    {
        Assert.Equal("INR One Thousand Two Hundred Thirty Four and Fifty Six Paise Only",
            IndianAmountInWords.ForRupee(1234.56m));
    }

    [Fact]
    public void ForRupee_RoundsToTwoDecimals()
    {
        Assert.Equal("INR One Hundred Only", IndianAmountInWords.ForRupee(99.995m));
    }

    [Fact]
    public void GstStateCode_ExtractsFromGstin()
    {
        Assert.Equal("36", GstStateCodeResolver.ExtractStateCode("36AABCU9603R1ZM"));
    }

    [Fact]
    public void GstStateCode_FormatStateLine()
    {
        var line = GstStateCodeResolver.FormatStateLine("Telangana", "36AABCU9603R1ZM");
        Assert.Equal("State Name : Telangana, Code : 36", line);
    }
}
