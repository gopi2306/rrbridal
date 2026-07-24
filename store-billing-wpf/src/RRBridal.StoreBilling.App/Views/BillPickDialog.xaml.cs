using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;
using System.Windows.Input;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Views;

public sealed class BillPickEntry
{
    public string BillNo { get; init; } = "";
    public string BillDate { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public decimal Payable { get; init; }
    public string PayableDisplay => MoneyMath.FormatRupee(Payable);

    public static BillPickEntry FromBson(BsonDocument doc)
    {
        var payable = doc.Contains("payable") ? (decimal)doc["payable"].ToDouble() : 0m;
        return new BillPickEntry
        {
            BillNo = doc.GetValue("billNo", "").AsString,
            BillDate = doc.GetValue("billDate", "").AsString,
            CustomerName = doc.GetValue("customerName", "").AsString,
            Payable = payable,
        };
    }
}

public partial class BillPickDialog : Window, INotifyPropertyChanged
{
    private BillPickEntry? _selectedBill;

    public ObservableCollection<BillPickEntry> Bills { get; } = new();

    public BillPickEntry? SelectedBill
    {
        get => _selectedBill;
        set
        {
            _selectedBill = value;
            OnPropertyChanged();
        }
    }

    public string? SelectedBillNo { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public BillPickDialog(IEnumerable<BsonDocument> documents)
    {
        InitializeComponent();
        DataContext = this;
        foreach (var doc in documents)
            Bills.Add(BillPickEntry.FromBson(doc));
        if (Bills.Count > 0)
            SelectedBill = Bills[0];
    }

    private void Select_OnClick(object sender, RoutedEventArgs e) => TryClose(true);

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Results_OnMouseDoubleClick(object sender, MouseButtonEventArgs e) => TryClose(true);

    private void TryClose(bool success)
    {
        if (SelectedBill == null)
        {
            if (success)
            {
                AppDialog.Show("Select a bill from the list.", "Select bill",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            return;
        }

        SelectedBillNo = SelectedBill.BillNo;
        DialogResult = true;
        Close();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
