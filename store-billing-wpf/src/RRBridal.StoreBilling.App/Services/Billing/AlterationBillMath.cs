namespace RRBridal.StoreBilling.App.Services.Billing;

/// <summary>Bill-level alteration total application (GST exclusive or inclusive).</summary>
public static class AlterationBillMath
{
    public readonly record struct AlterationLine(decimal Amount, decimal TaxPercent, bool IsIgst);

    public static decimal SumAlteration(IEnumerable<AlterationLine> lines) =>
        lines.Sum(l => l.Amount);

    public static void Apply(
        bool gstIncluded,
        IReadOnlyList<AlterationLine> lines,
        ref decimal revisedSub,
        ref decimal cgst,
        ref decimal sgst,
        ref decimal igst,
        ref decimal grandBeforeRound)
    {
        var alterationTotal = SumAlteration(lines);
        if (alterationTotal <= 0)
            return;

        if (gstIncluded)
        {
            foreach (var line in lines.Where(l => l.Amount > 0))
            {
                var split = BillingDiscountCalculator.ReverseSplitFromInclusive(
                    line.Amount, line.TaxPercent, line.IsIgst);
                revisedSub += split.Taxable;
                cgst += split.Cgst;
                sgst += split.Sgst;
                igst += split.Igst;
                grandBeforeRound += split.Inclusive;
            }
        }
        else
        {
            grandBeforeRound += alterationTotal;
        }
    }
}
