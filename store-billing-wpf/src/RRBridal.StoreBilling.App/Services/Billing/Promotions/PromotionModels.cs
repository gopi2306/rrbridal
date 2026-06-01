namespace RRBridal.StoreBilling.App.Services.Billing.Promotions;

public sealed class PromotionTimeWindow
{
    public int DayOfWeek { get; init; }
    public int FromHour { get; init; }
    public int ToHour { get; init; }
}

public sealed class PromotionComboRequirement
{
    public string Sku { get; init; } = "";
    public decimal RequiredQty { get; init; }
}

public sealed class PromotionConditions
{
    public IReadOnlyList<string> Skus { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> CategoryIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> BrandIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> OfferGroupIds { get; init; } = Array.Empty<string>();
    public decimal? MinLineQty { get; init; }
    public decimal? MinBillAmount { get; init; }
    public IReadOnlyList<string> CustomerTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> CustomerCodes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<PromotionComboRequirement> RequiredSkus { get; init; } = Array.Empty<PromotionComboRequirement>();
}

public sealed class PromotionSlab
{
    public decimal FromAmount { get; init; }
    public decimal? ToAmount { get; init; }
    public decimal DiscountPercent { get; init; }
}

public sealed class PromotionBenefit
{
    public string Mode { get; init; } = "";
    public decimal BuyQty { get; init; }
    public decimal GetQty { get; init; }
    public string FreeOn { get; init; } = "cheapest";
    public decimal DiscountPercent { get; init; }
    public decimal FlatAmount { get; init; }
    public decimal MinBillAmount { get; init; }
    public decimal FixedPrice { get; init; }
    public IReadOnlyList<string> ComboSkus { get; init; } = Array.Empty<string>();
    public IReadOnlyList<PromotionSlab> Slabs { get; init; } = Array.Empty<PromotionSlab>();
}

public sealed class PromotionSchemeDefinition
{
    public string Id { get; init; } = "";
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "scheme";
    public string Type { get; init; } = "";
    public int Priority { get; init; } = 100;
    public bool IsActive { get; init; } = true;
    public string Stacking { get; init; } = "best_benefit";
    public IReadOnlyList<string> StoreIds { get; init; } = Array.Empty<string>();
    public DateTime? ValidFrom { get; init; }
    public DateTime? ValidTo { get; init; }
    public IReadOnlyList<PromotionTimeWindow> TimeWindows { get; init; } = Array.Empty<PromotionTimeWindow>();
    public PromotionConditions Conditions { get; init; } = new();
    public PromotionBenefit Benefit { get; init; } = new();
}

public sealed class BillLineContext
{
    public int LineNo { get; init; }
    public string Sku { get; init; } = "";
    public string? CategoryId { get; init; }
    public string? BrandId { get; init; }
    public string? OfferGroupId { get; init; }
    public decimal Qty { get; init; }
    public decimal Rate { get; init; }
    public decimal Amount { get; init; }
    public decimal TaxPercent { get; init; }
    public bool IsIgst { get; init; }
    public decimal OriginalInclusive { get; init; }
}

public sealed class BillContext
{
    public IReadOnlyList<BillLineContext> Lines { get; init; } = Array.Empty<BillLineContext>();
    public string? CustomerCode { get; init; }
    public string? CustomerType { get; init; }
    public string StoreId { get; init; } = "";
    public DateTime BillDateTime { get; init; }
    public decimal Subtotal { get; init; }
    public decimal InclusiveTotal { get; init; }
    public IReadOnlySet<string> ExcludedSchemeCodes { get; init; } = new HashSet<string>();
}

public sealed class LineSchemeAdjustment
{
    public int LineNo { get; init; }
    public decimal SchemeDiscountAmount { get; init; }
}

public sealed class AppliedScheme
{
    public string SchemeCode { get; init; } = "";
    public string SchemeName { get; init; } = "";
    public decimal SavedAmount { get; init; }
}

public sealed class PromotionResult
{
    public IReadOnlyList<AppliedScheme> AppliedSchemes { get; init; } = Array.Empty<AppliedScheme>();
    public IReadOnlyList<LineSchemeAdjustment> LineAdjustments { get; init; } = Array.Empty<LineSchemeAdjustment>();
    public decimal BillAdjustment { get; init; }

    public static PromotionResult Empty { get; } = new();
}
