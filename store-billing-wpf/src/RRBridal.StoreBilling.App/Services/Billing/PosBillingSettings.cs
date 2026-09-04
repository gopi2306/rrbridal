using System;
using System.Collections.Generic;
using System.Linq;

namespace RRBridal.StoreBilling.App.Services.Billing;

/// <summary>How product Rate is interpreted on POS bills.</summary>
public enum BillPriceGstMode
{
    /// <summary>Rate includes GST; tax is reverse-split from Amount.</summary>
    WithGst = 0,

    /// <summary>Rate excludes GST; GST is added on top (forward tax).</summary>
    WithoutGst = 1,
}

public sealed class PosBillingSettingsDocument
{
    /// <summary>When true, call central APIs immediately; when false, work local-only and sync later.</summary>
    public bool PreferCentralOnline { get; set; }

    public bool AllowDuplicatePrint { get; set; } = true;

    /// <summary>When true, adding an SKU already on the bill asks before increasing its quantity.</summary>
    public bool ConfirmDuplicateProductAdd { get; set; } = true;

    /// <summary>When true, Payment dialog can pay out credit note remaining balance as cash.</summary>
    public bool AllowCreditNoteRemainingCashout { get; set; }

    /// <summary>How many billing line-item and product-pick columns to show.</summary>
    public BillingLineItemDetailLevel LineItemDetailLevel { get; set; } = BillingLineItemDetailLevel.Full;

    /// <summary>When true, alteration amounts are GST-inclusive and split using each line's tax %.</summary>
    public bool AlterationGstIncluded { get; set; }

    /// <summary>
    /// WithGst (default): Rate is GST-inclusive selling price.
    /// WithoutGst: Rate is exclusive selling price; GST is added on top.
    /// </summary>
    public BillPriceGstMode PriceGstMode { get; set; } = BillPriceGstMode.WithGst;

    /// <summary>When true, a bill may have multiple partial return transactions; fully returned lines are disabled.</summary>
    public bool AllowMultipleReturnsPerBill { get; set; }

    /// <summary>Master switch for Bill on credit (pay-later) option.</summary>
    public bool EnableCreditBilling { get; set; } = true;

    /// <summary>When true, only customers marked isCreditCustomer can use Bill on credit.</summary>
    public bool CreditBillingRequireCreditCustomer { get; set; } = true;

    /// <summary>Minimum advance percent of payable at post (0 = no percent minimum).</summary>
    public decimal CreditBillingMinimumAdvancePercent { get; set; }

    /// <summary>Minimum advance amount in ₹ at post (0 = no amount minimum).</summary>
    public decimal CreditBillingMinimumAdvanceAmount { get; set; }

    /// <summary>When true, full payable may be put on credit with zero advance.</summary>
    public bool CreditBillingAllowZeroAdvance { get; set; } = true;

    /// <summary>When true, later collection may be partial; when false, only full balance settlement.</summary>
    public bool CreditBillingAllowPartialCollection { get; set; } = true;

    /// <summary>Max balance due per bill (0 = no limit).</summary>
    public decimal CreditBillingMaxBalancePerBill { get; set; }

    /// <summary>
    /// Default Going Out of Stock threshold when product MOQ/min/reorder is unset or ≤ 0.
    /// SKUs appear when store qty is at or below this value. 0 disables the store-wide fallback.
    /// </summary>
    public decimal GoingOutOfStockMatchQty { get; set; } = 5m;

    /// <summary>When true, Card / Split-with-card payments may add a card processing charge.</summary>
    public bool EnableCardCharge { get; set; }

    /// <summary>Percent of card base amount charged when EnableCardCharge is true (0 = none).</summary>
    public decimal CardChargePercent { get; set; }

    /// <summary>Flat ₹ added when card base &gt; 0 and EnableCardCharge is true (0 = none).</summary>
    public decimal CardChargeFlatAmount { get; set; }

    /// <summary>Admin (counter 1) matrix: which counters may open which screens.</summary>
    public CounterScreenAccessSettings ScreenAccess { get; set; } = new();
}

/// <summary>
/// Allowed POS counter ids per Go To screen. Empty/null list falls back to defaults.
/// Settings is always counter "1" only (enforced in CanAccess / UI).
/// </summary>
public sealed class CounterScreenAccessSettings
{
    public static readonly string[] AllScreenKeys =
    [
        nameof(Billing),
        nameof(Vouchers),
        nameof(Quotations),
        nameof(Barcodes),
        nameof(Dashboard),
        nameof(Analytics),
        nameof(CustomerBillingReport),
        nameof(OnlineSales),
        nameof(CreditBills),
        nameof(Customers),
        nameof(Salesman),
        nameof(Ledger),
        nameof(Returns),
        nameof(BillLookup),
        nameof(OutboundDispatch),
        nameof(DayClose),
        nameof(Duplicate),
        nameof(Adjustments),
        nameof(DailyExpenses),
        nameof(Settings),
    ];

    public List<string> KnownCounters { get; set; } = new() { "1", "2", "3" };

    public List<string> Billing { get; set; } = new() { "1", "2", "3" };
    public List<string> Vouchers { get; set; } = new() { "1", "2", "3" };
    public List<string> Quotations { get; set; } = new() { "1", "2", "3" };
    public List<string> Barcodes { get; set; } = new() { "1", "2", "3" };
    public List<string> Dashboard { get; set; } = new() { "1" };
    public List<string> Analytics { get; set; } = new() { "1" };
    public List<string> CustomerBillingReport { get; set; } = new() { "1" };
    public List<string> OnlineSales { get; set; } = new() { "1" };
    public List<string> CreditBills { get; set; } = new() { "1" };
    public List<string> Customers { get; set; } = new() { "1", "2", "3" };
    public List<string> Salesman { get; set; } = new() { "1", "2", "3" };
    public List<string> Ledger { get; set; } = new() { "1" };
    public List<string> Returns { get; set; } = new() { "1", "2", "3" };
    public List<string> BillLookup { get; set; } = new() { "1", "2", "3" };
    public List<string> OutboundDispatch { get; set; } = new() { "1", "2", "3" };
    public List<string> DayClose { get; set; } = new() { "1", "2", "3" };
    public List<string> Duplicate { get; set; } = new() { "1", "2", "3" };
    public List<string> Adjustments { get; set; } = new() { "1", "2", "3" };
    public List<string> DailyExpenses { get; set; } = new() { "1" };
    /// <summary>Stored for display; runtime always forces counter 1 only.</summary>
    public List<string> Settings { get; set; } = new() { "1" };

    public static CounterScreenAccessSettings CreateDefaults() => new();

    public IReadOnlyList<string> AllowedFor(string screenKey)
    {
        var list = GetList(screenKey);
        if (list == null || list.Count == 0)
            return DefaultAllowed(screenKey);

        var normalized = Normalize(list);
        // POS 1 always retains Settings access.
        if (string.Equals(screenKey, nameof(Settings), StringComparison.Ordinal)
            && !normalized.Any(c => string.Equals(c, "1", StringComparison.OrdinalIgnoreCase)))
        {
            normalized = normalized.Prepend("1").ToList();
        }

        return normalized;
    }

    public bool IsCounterAllowed(string screenKey, string posCounter)
    {
        var counter = (posCounter ?? "").Trim();
        if (string.IsNullOrEmpty(counter))
            counter = "1";

        return AllowedFor(screenKey).Any(c =>
            string.Equals(c, counter, StringComparison.OrdinalIgnoreCase));
    }

    public void SetAllowed(string screenKey, IEnumerable<string> counters)
    {
        var normalized = Normalize(counters).ToList();
        if (string.Equals(screenKey, nameof(Settings), StringComparison.Ordinal)
            && !normalized.Any(c => string.Equals(c, "1", StringComparison.OrdinalIgnoreCase)))
        {
            normalized.Insert(0, "1");
        }

        switch (screenKey)
        {
            case nameof(Billing): Billing = normalized; break;
            case nameof(Vouchers): Vouchers = normalized; break;
            case nameof(Quotations): Quotations = normalized; break;
            case nameof(Barcodes): Barcodes = normalized; break;
            case nameof(Dashboard): Dashboard = normalized; break;
            case nameof(Analytics): Analytics = normalized; break;
            case nameof(CustomerBillingReport): CustomerBillingReport = normalized; break;
            case nameof(OnlineSales): OnlineSales = normalized; break;
            case nameof(CreditBills): CreditBills = normalized; break;
            case nameof(Customers): Customers = normalized; break;
            case nameof(Salesman): Salesman = normalized; break;
            case nameof(Ledger): Ledger = normalized; break;
            case nameof(Returns): Returns = normalized; break;
            case nameof(BillLookup): BillLookup = normalized; break;
            case nameof(OutboundDispatch): OutboundDispatch = normalized; break;
            case nameof(DayClose): DayClose = normalized; break;
            case nameof(Duplicate): Duplicate = normalized; break;
            case nameof(Adjustments): Adjustments = normalized; break;
            case nameof(DailyExpenses): DailyExpenses = normalized; break;
            case nameof(Settings): Settings = normalized; break;
        }
    }

    private List<string>? GetList(string screenKey) => screenKey switch
    {
        nameof(Billing) => Billing,
        nameof(Vouchers) => Vouchers,
        nameof(Quotations) => Quotations,
        nameof(Barcodes) => Barcodes,
        nameof(Dashboard) => Dashboard,
        nameof(Analytics) => Analytics,
        nameof(CustomerBillingReport) => CustomerBillingReport,
        nameof(OnlineSales) => OnlineSales,
        nameof(CreditBills) => CreditBills,
        nameof(Customers) => Customers,
        nameof(Salesman) => Salesman,
        nameof(Ledger) => Ledger,
        nameof(Returns) => Returns,
        nameof(BillLookup) => BillLookup,
        nameof(OutboundDispatch) => OutboundDispatch,
        nameof(DayClose) => DayClose,
        nameof(Duplicate) => Duplicate,
        nameof(Adjustments) => Adjustments,
        nameof(DailyExpenses) => DailyExpenses,
        nameof(Settings) => Settings,
        _ => null,
    };

    private static IReadOnlyList<string> DefaultAllowed(string screenKey) =>
        screenKey switch
        {
            nameof(Dashboard) or nameof(Analytics) or nameof(CustomerBillingReport)
                or nameof(OnlineSales) or nameof(CreditBills)
                or nameof(Ledger) or nameof(DailyExpenses) or nameof(Settings) => new[] { "1" },
            _ => new[] { "1", "2", "3" },
        };

    private static List<string> Normalize(IEnumerable<string> counters) =>
        counters
            .Select(c => (c ?? "").Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => int.TryParse(c, out var n) ? n : int.MaxValue)
            .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
