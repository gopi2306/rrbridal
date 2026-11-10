using System.Text.Json;
using RRBridal.StoreBilling.App.Services.Invoicing;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public sealed class ReceiptConfigSyncServiceTests
{
    [Fact]
    public void BuildCentralCompanyBankPayload_maps_store_bank_fields()
    {
        var store = new StoreProfile
        {
            BankAccountHolderName = "RR Bridal Pvt Ltd",
            BankAccountNumber = "1234567890",
            BankIfsc = "HDFC0001234",
            BankBranchName = "Madhapur",
        };

        var json = JsonSerializer.Serialize(ReceiptConfigSyncService.BuildCentralCompanyBankPayload(store));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("RR Bridal Pvt Ltd", root.GetProperty("bankAccountHolderName").GetString());
        Assert.Equal("1234567890", root.GetProperty("bankAccountNumber").GetString());
        Assert.Equal("HDFC0001234", root.GetProperty("bankIfsc").GetString());
        Assert.Equal("Madhapur", root.GetProperty("bankBranchName").GetString());
    }

    [Fact]
    public void BuildCentralCompanyBankPayload_uses_empty_strings_when_bank_fields_missing()
    {
        var json = JsonSerializer.Serialize(ReceiptConfigSyncService.BuildCentralCompanyBankPayload(new StoreProfile()));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("", root.GetProperty("bankAccountHolderName").GetString());
        Assert.Equal("", root.GetProperty("bankAccountNumber").GetString());
        Assert.Equal("", root.GetProperty("bankIfsc").GetString());
        Assert.Equal("", root.GetProperty("bankBranchName").GetString());
    }
}
