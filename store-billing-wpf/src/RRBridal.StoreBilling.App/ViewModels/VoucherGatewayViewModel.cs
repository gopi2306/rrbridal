using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RRBridal.StoreBilling.App.ViewModels;

public enum VoucherKind
{
    Sales,
    Receipt,
    Payment,
    CreditNote,
    Journal,
    DailyExpense,
}

public sealed class VoucherTypeItem
{
    public VoucherTypeItem(VoucherKind kind, string shortcutKey, string title, string subtitle)
    {
        Kind = kind;
        ShortcutKey = shortcutKey;
        Title = title;
        Subtitle = subtitle;
    }

    public VoucherKind Kind { get; }
    public string ShortcutKey { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string DisplayLine => $"{ShortcutKey}.  {Title}";
}

/// <summary>Tally-style voucher type picker that routes into existing POS screens.</summary>
public partial class VoucherGatewayViewModel : ObservableObject
{
    [ObservableProperty] private VoucherTypeItem? _selectedType;
    [ObservableProperty] private string _statusMessage = "Select a voucher type · Enter to open · 1–6 quick keys";

    public ObservableCollection<VoucherTypeItem> Types { get; } = new();

    /// <summary>Set by shell: open the mapped screen / dialog for this voucher kind.</summary>
    public Action<VoucherKind>? OpenVoucher { get; set; }

    /// <summary>Set by shell: open COD receipt collection (Online Sales).</summary>
    public Action? OpenCodReceipt { get; set; }

    public VoucherGatewayViewModel()
    {
        Types.Add(new VoucherTypeItem(
            VoucherKind.Sales, "1", "Sales",
            "Opens Billing — post a store sale invoice"));
        Types.Add(new VoucherTypeItem(
            VoucherKind.Receipt, "2", "Receipt",
            "Opens Credit Bills — collect customer AR (party receipt)"));
        Types.Add(new VoucherTypeItem(
            VoucherKind.Payment, "3", "Payment",
            "Cash withdrawal voucher — post via cash movement"));
        Types.Add(new VoucherTypeItem(
            VoucherKind.CreditNote, "4", "Credit Note",
            "Bill return or direct customer credit (no bill)"));
        Types.Add(new VoucherTypeItem(
            VoucherKind.Journal, "5", "Journal",
            "Opens Adjustments — bill value / stock journal-style adjust"));
        Types.Add(new VoucherTypeItem(
            VoucherKind.DailyExpense, "6", "Daily Expense",
            "Opens Expenses — post a daily expense slip"));

        SelectedType = Types[0];
    }

    [RelayCommand]
    private void OpenSelected()
    {
        if (SelectedType == null)
        {
            StatusMessage = "Select a voucher type first.";
            return;
        }

        OpenVoucher?.Invoke(SelectedType.Kind);
        StatusMessage = $"Opened {SelectedType.Title}.";
    }

    [RelayCommand]
    private void OpenType(VoucherTypeItem? item)
    {
        if (item == null)
            return;
        SelectedType = item;
        OpenSelected();
    }

    [RelayCommand]
    private void SelectByShortcut(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        var match = default(VoucherTypeItem);
        foreach (var t in Types)
        {
            if (string.Equals(t.ShortcutKey, key.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                match = t;
                break;
            }
        }

        if (match == null)
            return;

        SelectedType = match;
        OpenSelected();
    }

    [RelayCommand]
    private void OpenCodReceiptVoucher()
    {
        OpenCodReceipt?.Invoke();
        StatusMessage = "Opened Online Sales for COD receipt.";
    }
}
