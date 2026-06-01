using System.Globalization;

namespace RRBridal.StoreBilling.App.Services.Billing.Promotions;

public sealed class PromotionEngine
{
    public const decimal MaxStackDiscountPercent = 50m;

    private readonly PromotionSchemeRepository? _repository;

    public PromotionEngine(PromotionSchemeRepository? repository = null)
    {
        _repository = repository;
    }

    public PromotionResult Evaluate(BillContext context) =>
        Evaluate(context, _repository?.LoadActive() ?? Array.Empty<PromotionSchemeDefinition>());

    public PromotionResult Evaluate(BillContext context, IReadOnlyList<PromotionSchemeDefinition> schemes)
    {
        var active = schemes
            .Where(s => IsSchemeEligible(s, context))
            .ToList();

        if (active.Count == 0) return PromotionResult.Empty;

        var lineDiscounts = context.Lines.ToDictionary(l => l.LineNo, _ => 0m);
        var applied = new List<AppliedScheme>();
        var usedLineNos = new HashSet<int>();

        var itemSchemes = active.Where(s => s.Type == "item").ToList();
        ApplyItemStage(itemSchemes, context, lineDiscounts, applied, usedLineNos);

        var comboSchemes = active.Where(s => s.Type == "combo").ToList();
        ApplyComboStage(comboSchemes, context, lineDiscounts, applied, usedLineNos);

        var inclusiveAfterItemCombo = context.InclusiveTotal - lineDiscounts.Values.Sum();
        var billSchemes = active.Where(s => s.Type is "bill" or "slab").ToList();
        var billDiscount = ApplyBillStage(billSchemes, context, inclusiveAfterItemCombo, applied);

        var lineAdjustments = lineDiscounts
            .Where(kv => kv.Value > 0)
            .Select(kv => new LineSchemeAdjustment { LineNo = kv.Key, SchemeDiscountAmount = kv.Value })
            .ToList();

        return new PromotionResult
        {
            AppliedSchemes = applied,
            LineAdjustments = lineAdjustments,
            BillAdjustment = billDiscount,
        };
    }

    private static bool IsSchemeEligible(PromotionSchemeDefinition scheme, BillContext context)
    {
        if (!scheme.IsActive) return false;
        if (context.ExcludedSchemeCodes.Contains(scheme.Code)) return false;

        if (scheme.StoreIds.Count > 0 && !scheme.StoreIds.Contains(context.StoreId, StringComparer.OrdinalIgnoreCase))
            return false;

        var now = context.BillDateTime.ToUniversalTime();
        if (scheme.ValidFrom.HasValue && now < scheme.ValidFrom.Value) return false;
        if (scheme.ValidTo.HasValue && now > scheme.ValidTo.Value) return false;

        if (!IsWithinTimeWindows(scheme, context.BillDateTime)) return false;

        var cond = scheme.Conditions;
        if (cond.CustomerCodes.Count > 0)
        {
            var code = context.CustomerCode?.Trim();
            if (string.IsNullOrEmpty(code) || !cond.CustomerCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        if (cond.CustomerTypes.Count > 0)
        {
            var ctype = context.CustomerType?.Trim();
            if (string.IsNullOrEmpty(ctype) || !cond.CustomerTypes.Contains(ctype, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        if (cond.MinBillAmount.HasValue && context.InclusiveTotal < cond.MinBillAmount.Value)
            return false;

        return true;
    }

    internal static bool IsWithinTimeWindows(PromotionSchemeDefinition scheme, DateTime billDateTime)
    {
        if (scheme.TimeWindows.Count == 0) return true;
        var local = billDateTime;
        var dow = (int)local.DayOfWeek;
        var hour = local.Hour;
        foreach (var w in scheme.TimeWindows)
        {
            if (w.DayOfWeek != dow) continue;
            if (w.FromHour <= w.ToHour)
            {
                if (hour >= w.FromHour && hour < w.ToHour) return true;
            }
            else if (hour >= w.FromHour || hour < w.ToHour)
            {
                return true;
            }
        }
        return false;
    }

    private static void ApplyItemStage(
        IReadOnlyList<PromotionSchemeDefinition> schemes,
        BillContext context,
        Dictionary<int, decimal> lineDiscounts,
        List<AppliedScheme> applied,
        HashSet<int> usedLineNos)
    {
        var candidates = new List<(PromotionSchemeDefinition Scheme, decimal Savings, Action Apply)>();

        foreach (var scheme in schemes)
        {
            var matchingLines = GetMatchingLines(context, scheme).Where(l => !usedLineNos.Contains(l.LineNo)).ToList();
            if (matchingLines.Count == 0) continue;

            if (scheme.Conditions.MinLineQty.HasValue)
            {
                var totalQty = matchingLines.Sum(l => l.Qty);
                if (totalQty < scheme.Conditions.MinLineQty.Value) continue;
            }

            var benefit = scheme.Benefit;
            if (benefit.Mode == "buy_x_get_y" && benefit.BuyQty > 0 && benefit.GetQty > 0)
            {
                var groupSize = benefit.BuyQty + benefit.GetQty;
                var totalQty = matchingLines.Sum(l => l.Qty);
                var sets = (int)Math.Floor(totalQty / groupSize);
                if (sets <= 0) continue;

                var freeUnits = (int)Math.Floor(sets * benefit.GetQty);
                if (freeUnits <= 0) continue;
                var unitPrices = ExpandUnits(matchingLines);
                var freeOnCheapest = !string.Equals(benefit.FreeOn, "highest", StringComparison.OrdinalIgnoreCase);
                var ordered = freeOnCheapest
                    ? unitPrices.OrderBy(u => u.UnitInclusive).ToList()
                    : unitPrices.OrderByDescending(u => u.UnitInclusive).ToList();

                var savings = ordered.Take(freeUnits).Sum(u => u.UnitInclusive);
                if (savings <= 0) continue;

                candidates.Add((scheme, savings, () =>
                {
                    DistributeLineDiscount(matchingLines, savings, lineDiscounts, ordered.Take(freeUnits).Select(u => u.LineNo).ToHashSet());
                    applied.Add(new AppliedScheme { SchemeCode = scheme.Code, SchemeName = scheme.Name, SavedAmount = savings });
                    foreach (var ln in matchingLines) usedLineNos.Add(ln.LineNo);
                }));
            }
            else if (benefit.Mode == "percent_off" && benefit.DiscountPercent > 0)
            {
                var baseInclusive = matchingLines.Sum(l => l.OriginalInclusive);
                var savings = MoneyMath.RoundAmount(baseInclusive * benefit.DiscountPercent / 100m);
                if (savings <= 0) continue;
                candidates.Add((scheme, savings, () =>
                {
                    DistributeProportional(matchingLines, savings, lineDiscounts);
                    applied.Add(new AppliedScheme { SchemeCode = scheme.Code, SchemeName = scheme.Name, SavedAmount = savings });
                    foreach (var ln in matchingLines) usedLineNos.Add(ln.LineNo);
                }));
            }
            else if (benefit.Mode == "flat_off" && benefit.FlatAmount > 0)
            {
                var savings = Math.Min(benefit.FlatAmount, matchingLines.Sum(l => l.OriginalInclusive));
                if (savings <= 0) continue;
                candidates.Add((scheme, savings, () =>
                {
                    DistributeProportional(matchingLines, savings, lineDiscounts);
                    applied.Add(new AppliedScheme { SchemeCode = scheme.Code, SchemeName = scheme.Name, SavedAmount = savings });
                    foreach (var ln in matchingLines) usedLineNos.Add(ln.LineNo);
                }));
            }
        }

        ResolveAndApply(candidates, context.InclusiveTotal, lineDiscounts);
    }

    private static void ApplyComboStage(
        IReadOnlyList<PromotionSchemeDefinition> schemes,
        BillContext context,
        Dictionary<int, decimal> lineDiscounts,
        List<AppliedScheme> applied,
        HashSet<int> usedLineNos)
    {
        var candidates = new List<(PromotionSchemeDefinition Scheme, decimal Savings, Action Apply)>();

        foreach (var scheme in schemes)
        {
            var comboSkus = scheme.Benefit.ComboSkus;
            if (comboSkus.Count < 2 || scheme.Benefit.FixedPrice <= 0) continue;

            var matchedLines = new List<BillLineContext>();
            foreach (var sku in comboSkus)
            {
                var line = context.Lines.FirstOrDefault(l =>
                    l.Qty > 0 &&
                    !usedLineNos.Contains(l.LineNo) &&
                    string.Equals(l.Sku, sku, StringComparison.OrdinalIgnoreCase));
                if (line == null) { matchedLines.Clear(); break; }
                matchedLines.Add(line);
            }

            if (matchedLines.Count != comboSkus.Count) continue;

            var comboInclusive = matchedLines.Sum(l => l.OriginalInclusive);
            var savings = MoneyMath.RoundAmount(comboInclusive - scheme.Benefit.FixedPrice);
            if (savings <= 0) continue;

            candidates.Add((scheme, savings, () =>
            {
                DistributeProportional(matchedLines, savings, lineDiscounts);
                applied.Add(new AppliedScheme { SchemeCode = scheme.Code, SchemeName = scheme.Name, SavedAmount = savings });
                foreach (var ln in matchedLines) usedLineNos.Add(ln.LineNo);
            }));
        }

        ResolveAndApply(candidates, context.InclusiveTotal, lineDiscounts);
    }

    private static decimal ApplyBillStage(
        IReadOnlyList<PromotionSchemeDefinition> schemes,
        BillContext context,
        decimal inclusiveAfterPrior,
        List<AppliedScheme> applied)
    {
        if (inclusiveAfterPrior <= 0) return 0;

        var candidates = new List<(PromotionSchemeDefinition Scheme, decimal Savings, Action Apply)>();

        foreach (var scheme in schemes)
        {
            var minBill = scheme.Conditions.MinBillAmount ?? scheme.Benefit.MinBillAmount;
            if (minBill > 0 && inclusiveAfterPrior < minBill) continue;

            decimal savings = 0;
            if (scheme.Type == "slab")
            {
                var slab = FindSlab(scheme.Benefit.Slabs, inclusiveAfterPrior);
                if (slab == null || slab.DiscountPercent <= 0) continue;
                savings = MoneyMath.RoundAmount(inclusiveAfterPrior * slab.DiscountPercent / 100m);
            }
            else if (scheme.Benefit.DiscountPercent > 0)
            {
                savings = MoneyMath.RoundAmount(inclusiveAfterPrior * scheme.Benefit.DiscountPercent / 100m);
            }
            else if (scheme.Benefit.FlatAmount > 0)
            {
                savings = Math.Min(scheme.Benefit.FlatAmount, inclusiveAfterPrior);
            }

            if (savings <= 0) continue;
            candidates.Add((scheme, savings, () =>
            {
                applied.Add(new AppliedScheme { SchemeCode = scheme.Code, SchemeName = scheme.Name, SavedAmount = savings });
            }));
        }

        if (candidates.Count == 0) return 0;

        var selected = SelectCandidates(candidates, context.InclusiveTotal, new Dictionary<int, decimal>());
        decimal total = 0;
        foreach (var c in selected)
        {
            c.Apply();
            total += c.Savings;
        }
        return total;
    }

    private static PromotionSlab? FindSlab(IReadOnlyList<PromotionSlab> slabs, decimal amount)
    {
        foreach (var slab in slabs.OrderBy(s => s.FromAmount))
        {
            if (amount < slab.FromAmount) continue;
            if (slab.ToAmount.HasValue && amount > slab.ToAmount.Value) continue;
            return slab;
        }
        return null;
    }

    private static IReadOnlyList<BillLineContext> GetMatchingLines(BillContext context, PromotionSchemeDefinition scheme)
    {
        var cond = scheme.Conditions;
        return context.Lines.Where(l =>
        {
            if (l.Qty <= 0 || l.Amount <= 0) return false;
            if (cond.Skus.Count > 0 && !cond.Skus.Any(s => string.Equals(s, l.Sku, StringComparison.OrdinalIgnoreCase)))
                return false;
            if (cond.CategoryIds.Count > 0 && (l.CategoryId == null || !cond.CategoryIds.Contains(l.CategoryId)))
                return false;
            if (cond.BrandIds.Count > 0 && (l.BrandId == null || !cond.BrandIds.Contains(l.BrandId)))
                return false;
            if (cond.OfferGroupIds.Count > 0 && (l.OfferGroupId == null || !cond.OfferGroupIds.Contains(l.OfferGroupId)))
                return false;
            return true;
        }).ToList();
    }

    private static List<(int LineNo, decimal UnitInclusive)> ExpandUnits(IReadOnlyList<BillLineContext> lines)
    {
        var units = new List<(int LineNo, decimal UnitInclusive)>();
        foreach (var line in lines)
        {
            var unitInc = line.Qty > 0 ? MoneyMath.RoundAmount(line.OriginalInclusive / line.Qty) : line.OriginalInclusive;
            var count = (int)Math.Floor(line.Qty);
            for (var i = 0; i < count; i++)
                units.Add((line.LineNo, unitInc));
        }
        return units;
    }

    private static void DistributeLineDiscount(
        IReadOnlyList<BillLineContext> lines,
        decimal totalSavings,
        Dictionary<int, decimal> lineDiscounts,
        HashSet<int> targetLineNos)
    {
        var targetLines = lines.Where(l => targetLineNos.Contains(l.LineNo)).ToList();
        if (targetLines.Count == 0)
        {
            DistributeProportional(lines, totalSavings, lineDiscounts);
            return;
        }

        var perLine = targetLines.GroupBy(l => l.LineNo).ToDictionary(g => g.Key, g => g.First());
        var targetInclusive = perLine.Values.Sum(l => l.OriginalInclusive);
        decimal allocated = 0;
        var ordered = perLine.Values.OrderBy(l => l.LineNo).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var line = ordered[i];
            var share = i == ordered.Count - 1
                ? totalSavings - allocated
                : targetInclusive > 0
                    ? MoneyMath.RoundAmount(totalSavings * line.OriginalInclusive / targetInclusive)
                    : 0;
            lineDiscounts[line.LineNo] = MoneyMath.RoundAmount(lineDiscounts.GetValueOrDefault(line.LineNo) + share);
            allocated += share;
        }
    }

    private static void DistributeProportional(
        IReadOnlyList<BillLineContext> lines,
        decimal totalSavings,
        Dictionary<int, decimal> lineDiscounts)
    {
        var baseTotal = lines.Sum(l => l.OriginalInclusive);
        if (baseTotal <= 0) return;
        decimal allocated = 0;
        var ordered = lines.OrderBy(l => l.LineNo).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var line = ordered[i];
            var share = i == ordered.Count - 1
                ? totalSavings - allocated
                : MoneyMath.RoundAmount(totalSavings * line.OriginalInclusive / baseTotal);
            lineDiscounts[line.LineNo] = MoneyMath.RoundAmount(lineDiscounts.GetValueOrDefault(line.LineNo) + share);
            allocated += share;
        }
    }

    private static void ResolveAndApply(
        List<(PromotionSchemeDefinition Scheme, decimal Savings, Action Apply)> candidates,
        decimal originalInclusive,
        Dictionary<int, decimal> lineDiscounts)
    {
        if (candidates.Count == 0) return;
        var selected = SelectCandidates(candidates, originalInclusive, lineDiscounts);
        foreach (var c in selected) c.Apply();
    }

    private static List<(PromotionSchemeDefinition Scheme, decimal Savings, Action Apply)> SelectCandidates(
        List<(PromotionSchemeDefinition Scheme, decimal Savings, Action Apply)> candidates,
        decimal originalInclusive,
        Dictionary<int, decimal> lineDiscounts)
    {
        if (candidates.Count == 0) return candidates;

        var stacking = candidates[0].Scheme.Stacking;
        if (stacking == "highest_priority")
        {
            var best = candidates.OrderBy(c => c.Scheme.Priority).ThenByDescending(c => c.Savings).First();
            return new List<(PromotionSchemeDefinition, decimal, Action)> { best };
        }

        if (stacking == "allow_stack")
        {
            var maxTotal = MoneyMath.RoundAmount(originalInclusive * MaxStackDiscountPercent / 100m);
            var ordered = candidates.OrderBy(c => c.Scheme.Priority).ThenByDescending(c => c.Savings).ToList();
            var picked = new List<(PromotionSchemeDefinition Scheme, decimal Savings, Action Apply)>();
            decimal running = lineDiscounts.Values.Sum();
            foreach (var c in ordered)
            {
                if (running + c.Savings > maxTotal) continue;
                picked.Add(c);
                running += c.Savings;
            }
            return picked;
        }

        // best_benefit (default)
        var winner = candidates.OrderByDescending(c => c.Savings).ThenBy(c => c.Scheme.Priority).First();
        return new List<(PromotionSchemeDefinition, decimal, Action)> { winner };
    }
}
