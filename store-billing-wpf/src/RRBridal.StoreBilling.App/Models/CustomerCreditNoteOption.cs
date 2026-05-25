using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RRBridal.StoreBilling.App.Models;

public partial class CustomerCreditNoteOption : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    public required string CreditNoteNo { get; init; }
    public required string ReturnNo { get; init; }
    public required decimal OriginalAmount { get; init; }
    public required decimal RemainingAmount { get; init; }

    [ObservableProperty] private decimal _applyingAmount;
    [ObservableProperty] private decimal _remainingAfterApply;
    [ObservableProperty] private bool _isSelected;

    public string DisplayLabel
    {
        get
        {
            if (IsSelected && ApplyingAmount > 0)
                return $"{CreditNoteNo} · Total {Fmt(OriginalAmount)} · Use {Fmt(ApplyingAmount)} · Left {Fmt(RemainingAfterApply)}";
            return $"{CreditNoteNo} · Balance {Fmt(RemainingAmount)}";
        }
    }

    public void RefreshDisplayLabel() => OnPropertyChanged(nameof(DisplayLabel));

    private static string Fmt(decimal v) => "₹ " + v.ToString("N2", InCulture);

    public static CustomerCreditNoteOption FromRecord(Services.Billing.CustomerCreditNoteRecord r) =>
        new()
        {
            CreditNoteNo = r.CreditNoteNo,
            ReturnNo = r.ReturnNo,
            OriginalAmount = r.Amount,
            RemainingAmount = r.RemainingAmount,
        };
}
