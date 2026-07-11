using System.Collections.Generic;
using System.Linq;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Maps Indian GST state codes (first two GSTIN digits) to state names.</summary>
public static class GstStateCodeResolver
{
    private static readonly IReadOnlyDictionary<string, string> CodeToName = new Dictionary<string, string>
    {
        ["01"] = "Jammu & Kashmir",
        ["02"] = "Himachal Pradesh",
        ["03"] = "Punjab",
        ["04"] = "Chandigarh",
        ["05"] = "Uttarakhand",
        ["06"] = "Haryana",
        ["07"] = "Delhi",
        ["08"] = "Rajasthan",
        ["09"] = "Uttar Pradesh",
        ["10"] = "Bihar",
        ["11"] = "Sikkim",
        ["12"] = "Arunachal Pradesh",
        ["13"] = "Nagaland",
        ["14"] = "Manipur",
        ["15"] = "Mizoram",
        ["16"] = "Tripura",
        ["17"] = "Meghalaya",
        ["18"] = "Assam",
        ["19"] = "West Bengal",
        ["20"] = "Jharkhand",
        ["21"] = "Odisha",
        ["22"] = "Chhattisgarh",
        ["23"] = "Madhya Pradesh",
        ["24"] = "Gujarat",
        ["25"] = "Daman & Diu",
        ["26"] = "Dadra & Nagar Haveli",
        ["27"] = "Maharashtra",
        ["28"] = "Andhra Pradesh",
        ["29"] = "Karnataka",
        ["30"] = "Goa",
        ["31"] = "Lakshadweep",
        ["32"] = "Kerala",
        ["33"] = "Tamil Nadu",
        ["34"] = "Puducherry",
        ["35"] = "Andaman & Nicobar Islands",
        ["36"] = "Telangana",
        ["37"] = "Andhra Pradesh",
        ["38"] = "Ladakh",
    };

    public static string? ExtractStateCode(string? gstin)
    {
        if (string.IsNullOrWhiteSpace(gstin) || gstin.Length < 2)
            return null;

        var code = gstin.Trim()[..2];
        return code.All(char.IsDigit) ? code : null;
    }

    public static string? ResolveStateName(string? stateCode)
    {
        if (string.IsNullOrWhiteSpace(stateCode))
            return null;

        return CodeToName.TryGetValue(stateCode.Trim(), out var name) ? name : null;
    }

    public static string FormatStateLine(string? stateName, string? gstin)
    {
        var code = ExtractStateCode(gstin);
        var name = !string.IsNullOrWhiteSpace(stateName)
            ? stateName.Trim()
            : ResolveStateName(code);

        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(code))
            return $"State Name : {name}, Code : {code}";

        if (!string.IsNullOrWhiteSpace(name))
            return $"State Name : {name}";

        if (!string.IsNullOrWhiteSpace(code))
            return $"Code : {code}";

        return "";
    }
}
