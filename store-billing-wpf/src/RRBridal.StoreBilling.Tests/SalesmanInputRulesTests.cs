using RRBridal.StoreBilling.App.Services.Salesmen;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class SalesmanInputRulesTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Gopi")]
    [InlineData("  ali  ")]
    [InlineData("98765")]
    [InlineData("9876543210")]
    [InlineData("S. D'Souza-Khan")]
    public void ValidateSearch_accepts_names_and_phone_numbers(string search) =>
        Assert.Null(SalesmanInputRules.ValidateSearch(search));

    [Theory]
    [InlineData("nckdknvkd37t8t8t8dbkbjsqw!@1233")]
    [InlineData("98765432101234")]
    [InlineData("gopi@123")]
    [InlineData("$%^&")]
    public void ValidateSearch_rejects_invalid_text(string search) =>
        Assert.NotNull(SalesmanInputRules.ValidateSearch(search));

    [Theory]
    [InlineData("")]
    [InlineData("9876543210")]
    [InlineData("6000000000")]
    public void ValidatePhone_accepts_empty_or_valid_mobile(string phone) =>
        Assert.Null(SalesmanInputRules.ValidatePhone(phone));

    [Theory]
    [InlineData("vbuyvyuyqvyufiRW^&@R")]
    [InlineData("98765")]
    [InlineData("98765432101")]
    [InlineData("1234567890")]
    public void ValidatePhone_rejects_invalid(string phone) =>
        Assert.NotNull(SalesmanInputRules.ValidatePhone(phone));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("dsjvsb!$E@$!E^$%^7fyshcj654665313586516")]
    [InlineData("Gopi1")]
    public void ValidateName_rejects_invalid(string name) =>
        Assert.NotNull(SalesmanInputRules.ValidateName(name));

    [Fact]
    public void ValidateName_accepts_normal_name() =>
        Assert.Null(SalesmanInputRules.ValidateName("Mohammed Ali"));

    [Fact]
    public void Search_input_stops_at_ten_digits() =>
        Assert.False(SalesmanInputRules.IsAcceptableSearchInput("98765432101"));

    [Fact]
    public void Phone_input_allows_only_digits() =>
        Assert.False(SalesmanInputRules.IsAcceptablePhoneInput("98a"));
}
