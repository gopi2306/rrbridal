using System.Text.RegularExpressions;

namespace RRBridal.StoreBilling.App.Services.Customers;

public enum GstinLookupApplyMode
{
    /// <summary>Copy GST fields only where the destination is blank.</summary>
    FillEmpty = 0,

    /// <summary>Overwrite address from GST; fill name only if it is blank.</summary>
    OverwriteAddress = 1,
}

public sealed class GstinLookupDetails
{
    public string Gstin { get; init; } = "";
    public string Name { get; init; } = "";
    public string LegalName { get; init; } = "";
    public string TradeName { get; init; } = "";
    public string Status { get; init; } = "";
    public bool IsActive { get; init; } = true;
    public string DoorNo { get; init; } = "";
    public string Street { get; init; } = "";
    public string FullAddress { get; init; } = "";
    public string City { get; init; } = "";
    public string State { get; init; } = "";
    public string Pincode { get; init; } = "";
}

public sealed class GstinLookupFieldSet
{
    public string Name { get; set; } = "";
    public string DoorNo { get; set; } = "";
    public string Street { get; set; } = "";
    public string FullAddress { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Pincode { get; set; } = "";
}

public static class GstinLookupMapper
{
    private static readonly Regex GstinPattern = new(
        "^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][0-9A-Z]Z[0-9A-Z]$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string Normalize(string? gstin) => (gstin ?? "").Trim().ToUpperInvariant();

    public static bool IsValidGstin(string? gstin) => GstinPattern.IsMatch(Normalize(gstin));

    /// <summary>Returns an error when GSTIN is not ready to call the API; otherwise null.</summary>
    public static string? ValidateForLookup(string? gstin)
    {
        var normalized = Normalize(gstin);
        if (string.IsNullOrWhiteSpace(normalized))
            return "Enter a GSTIN to search.";
        if (!IsValidGstin(normalized))
            return "Enter a valid 15-character GSTIN.";
        return null;
    }

    public static GstinLookupFieldSet Apply(
        GstinLookupDetails source,
        GstinLookupFieldSet current,
        GstinLookupApplyMode mode)
    {
        var dest = new GstinLookupFieldSet
        {
            Name = current.Name ?? "",
            DoorNo = current.DoorNo ?? "",
            Street = current.Street ?? "",
            FullAddress = current.FullAddress ?? "",
            City = current.City ?? "",
            State = current.State ?? "",
            Pincode = current.Pincode ?? "",
        };

        dest.Name = FillOrKeep(dest.Name, source.Name, overwrite: false);
        var overwriteAddress = mode == GstinLookupApplyMode.OverwriteAddress;
        dest.DoorNo = FillOrKeep(dest.DoorNo, source.DoorNo, overwriteAddress);
        dest.Street = FillOrKeep(dest.Street, source.Street, overwriteAddress);
        dest.FullAddress = FillOrKeep(dest.FullAddress, source.FullAddress, overwriteAddress);
        dest.City = FillOrKeep(dest.City, source.City, overwriteAddress);
        dest.State = FillOrKeep(dest.State, source.State, overwriteAddress);
        dest.Pincode = FillOrKeep(dest.Pincode, source.Pincode, overwriteAddress);
        return dest;
    }

    public static string StatusMessage(GstinLookupDetails details)
    {
        var name = string.IsNullOrWhiteSpace(details.Name) ? details.Gstin : details.Name;
        if (details.IsActive)
            return $"GSTIN found: {name}.";
        var status = string.IsNullOrWhiteSpace(details.Status) ? "not Active" : details.Status;
        return $"GSTIN found ({status}): {name}. Fields were filled — confirm before saving.";
    }

    private static string FillOrKeep(string current, string incoming, bool overwrite)
    {
        var value = (incoming ?? "").Trim();
        if (string.IsNullOrWhiteSpace(value))
            return current;
        if (overwrite || string.IsNullOrWhiteSpace(current))
            return value;
        return current;
    }
}
