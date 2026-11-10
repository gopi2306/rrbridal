using RRBridal.StoreBilling.App.Services.Customers;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public sealed class GstinLookupMapperTests
{
    private static GstinLookupDetails Sample() => new()
    {
        Gstin = "36AABCU9603R1ZM",
        Name = "RR Bridal",
        LegalName = "RR BRIDAL PRIVATE LIMITED",
        TradeName = "RR Bridal",
        Status = "Active",
        IsActive = true,
        DoorNo = "12-3",
        Street = "Bridal Plaza, Road No 1",
        FullAddress = "12-3, Bridal Plaza, Road No 1, Madhapur, Telangana, 500081",
        City = "Madhapur",
        State = "Telangana",
        Pincode = "500081",
    };

    [Fact]
    public void ValidateForLookup_rejects_invalid_gstin()
    {
        Assert.NotNull(GstinLookupMapper.ValidateForLookup("36INVALID"));
        Assert.NotNull(GstinLookupMapper.ValidateForLookup(""));
        Assert.Null(GstinLookupMapper.ValidateForLookup("36AABCU9603R1ZM"));
    }

    [Fact]
    public void Guard_skips_api_when_gstin_is_invalid()
    {
        var error = GstinLookupService.Guard(enabled: true, online: true, gstin: "BAD");
        Assert.Equal("Enter a valid 15-character GSTIN.", error);
    }

    [Fact]
    public void Guard_skips_api_when_disabled_or_offline()
    {
        Assert.Contains("turned off", GstinLookupService.Guard(false, true, "36AABCU9603R1ZM"));
        Assert.Contains("Online", GstinLookupService.Guard(true, false, "36AABCU9603R1ZM"));
        Assert.Null(GstinLookupService.Guard(true, true, "36AABCU9603R1ZM"));
    }

    [Fact]
    public void FillEmpty_does_not_overwrite_existing_fields()
    {
        var current = new GstinLookupFieldSet
        {
            Name = "Walk-in",
            DoorNo = "9",
            City = "Hyderabad",
        };

        var applied = GstinLookupMapper.Apply(Sample(), current, GstinLookupApplyMode.FillEmpty);

        Assert.Equal("Walk-in", applied.Name);
        Assert.Equal("9", applied.DoorNo);
        Assert.Equal("Bridal Plaza, Road No 1", applied.Street);
        Assert.Equal("Hyderabad", applied.City);
        Assert.Equal("Telangana", applied.State);
        Assert.Equal("500081", applied.Pincode);
        Assert.Contains("Madhapur", applied.FullAddress);
    }

    [Fact]
    public void OverwriteAddress_replaces_address_but_keeps_existing_name()
    {
        var current = new GstinLookupFieldSet
        {
            Name = "Walk-in",
            DoorNo = "9",
            Street = "Old street",
            FullAddress = "old",
            City = "Hyderabad",
            State = "Old",
            Pincode = "000000",
        };

        var applied = GstinLookupMapper.Apply(Sample(), current, GstinLookupApplyMode.OverwriteAddress);

        Assert.Equal("Walk-in", applied.Name);
        Assert.Equal("12-3", applied.DoorNo);
        Assert.Equal("Bridal Plaza, Road No 1", applied.Street);
        Assert.Equal("Madhapur", applied.City);
        Assert.Equal("Telangana", applied.State);
        Assert.Equal("500081", applied.Pincode);
        Assert.Contains("Madhapur", applied.FullAddress);
    }

    [Fact]
    public void FillEmpty_fills_blank_name_from_gst()
    {
        var applied = GstinLookupMapper.Apply(
            Sample(),
            new GstinLookupFieldSet(),
            GstinLookupApplyMode.FillEmpty);

        Assert.Equal("RR Bridal", applied.Name);
        Assert.Equal("12-3", applied.DoorNo);
    }
}
