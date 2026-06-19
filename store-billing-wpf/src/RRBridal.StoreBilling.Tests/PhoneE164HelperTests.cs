using RRBridal.StoreBilling.App.Services.Customers;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class PhoneE164HelperTests
{
    [Theory]
    [InlineData("9876543210", "91", "919876543210")]
    [InlineData("+91 98765 43210", "91", "919876543210")]
    [InlineData("919876543210", "91", "919876543210")]
    public void ToWhatsAppE164_normalizes_indian_numbers(string input, string cc, string expected)
    {
        Assert.Equal(expected, PhoneE164Helper.ToWhatsAppE164(input, cc));
    }

    [Fact]
    public void CanSendWhatsApp_requires_valid_number()
    {
        Assert.True(PhoneE164Helper.CanSendWhatsApp("9876543210"));
        Assert.False(PhoneE164Helper.CanSendWhatsApp("123"));
        Assert.False(PhoneE164Helper.CanSendWhatsApp(""));
    }
}
