using RRBridal.StoreBilling.App.Services.Customers;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class CustomerInputRulesTests
{
    [Theory]
    [InlineData("", true)]
    [InlineData("9876543210", true)]
    [InlineData("98765432101", false)]
    [InlineData("98765a", false)]
    [InlineData("98765 4", false)]
    [InlineData("９８７", false)]
    public void Mobile(string text, bool ok) =>
        Assert.Equal(ok, CustomerInputRules.IsAcceptableMobile(text));

    [Theory]
    [InlineData("CUST-0001", true)]
    [InlineData("7C8A0175", true)]
    [InlineData("CUST-00001", false)]
    [InlineData("CUST_01", false)]
    [InlineData("ÇUST", false)]
    public void CustomerCode(string text, bool ok) =>
        Assert.Equal(ok, CustomerInputRules.IsAcceptableCustomerCode(text));

    [Theory]
    [InlineData("Bilal Textiles", true)]
    [InlineData("S. D'Souza-Khan & Sons", true)]
    [InlineData("Shop 24", true)]
    [InlineData("Ravi@123", false)]
    [InlineData("Ravi 😀", false)]
    [InlineData("रवि", false)]
    public void Name(string text, bool ok) =>
        Assert.Equal(ok, CustomerInputRules.IsAcceptableName(text));

    [Theory]
    [InlineData("36DHPPM4427L2ZP", true)]
    [InlineData("36DHPPM4427L2ZP1", false)]
    [InlineData("36-DHPPM", false)]
    [InlineData("36 DH", false)]
    public void Gstin(string text, bool ok) =>
        Assert.Equal(ok, CustomerInputRules.IsAcceptableGstin(text));

    [Theory]
    [InlineData("040-23456789", true)]
    [InlineData("040 2345 6789", true)]
    [InlineData("040-2345-6789-12", false)]
    [InlineData("040x", false)]
    public void Telephone(string text, bool ok) =>
        Assert.Equal(ok, CustomerInputRules.IsAcceptableTelephone(text));
}
