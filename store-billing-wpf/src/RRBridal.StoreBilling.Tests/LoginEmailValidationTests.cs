using RRBridal.StoreBilling.App.ViewModels;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class LoginEmailValidationTests
{
    [Theory]
    [InlineData("ops-admin@rrbridal.com")]
    [InlineData("  ui.test@rrbridal.test  ")]
    [InlineData("first.last+tag@shop.co.in")]
    public void Accepts_valid_emails(string email) =>
        Assert.True(LoginViewModel.IsValidEmail(email));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("lddddddddddddddddddddddddddd")]
    [InlineData("name@")]
    [InlineData("@rrbridal.com")]
    [InlineData("name@rrbridal")]
    [InlineData("na me@rrbridal.com")]
    [InlineData("a@b@rrbridal.com")]
    public void Rejects_invalid_emails(string email) =>
        Assert.False(LoginViewModel.IsValidEmail(email));
}
