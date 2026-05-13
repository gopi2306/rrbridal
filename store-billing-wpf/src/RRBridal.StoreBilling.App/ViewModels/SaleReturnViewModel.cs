using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Models;
using RRBridal.StoreBilling.App.Services;

namespace RRBridal.StoreBilling.App.ViewModels;

public enum ReturnMode
{
    CreditNote,
    CashRefund,
}

public partial class SaleReturnViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    private readonly AppServices _services;

    [ObservableProperty] private string _originalBillNo = "";
    [ObservableProperty] private bool _billLoaded;
    [ObservableProperty] private string _reason = "";
    [ObservableProperty] private ReturnMode _returnMode = ReturnMode.CreditNote;

    [ObservableProperty] private string _returnNo = "";
    [ObservableProperty] private bool _isInterState;

    [ObservableProperty] private string _subTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _cgstTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _sgstTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _igstTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _returnTotalFormatted = "₹ 0.00";

    public ObservableCollection<SaleReturnLineItem> ReturnLines { get; } = new();

    private BsonDocument? _originalBillDoc;

    public SaleReturnViewModel(AppServices services)
    {
        _services = services;
        AssignReturnNo();
    }

    private void AssignReturnNo()
    {
        ReturnNo = $"RET-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(100, 999)}";
    }

    [RelayCommand]
    private async Task LookupBill()
    {
        var billNo = (OriginalBillNo ?? "").Trim();
        if (string.IsNullOrEmpty(billNo))
        {
            MessageBox.Show("Enter a bill number to look up.", "Sale Return", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var coll = _services.LocalDb.GetCollection<BsonDocument>("store_bills");
        var doc = await coll.Find(new BsonDocument("billNo", billNo)).FirstOrDefaultAsync();
        if (doc == null)
        {
            MessageBox.Show($"Bill '{billNo}' not found.", "Sale Return", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _originalBillDoc = doc;
        IsInterState = doc.Contains("isInterState") && doc["isInterState"].AsBoolean;
        ReturnLines.Clear();

        if (doc.Contains("lines") && doc["lines"].IsBsonArray)
        {
            foreach (BsonDocument lineBson in doc["lines"].AsBsonArray.OfType<BsonDocument>())
            {
                var item = new SaleReturnLineItem
                {
                    LineNo = lineBson.GetValue("lineNo", 0).ToInt32(),
                    ProductCode = lineBson.GetValue("sku", "").AsString,
                    Description = lineBson.GetValue("description", "").AsString,
                    OriginalQty = (decimal)lineBson.GetValue("qty", 0).ToDouble(),
                    Rate = (decimal)lineBson.GetValue("rate", 0).ToDouble(),
                    TaxPercent = (decimal)lineBson.GetValue("taxPercent", 0).ToDouble(),
                    IsIgst = IsInterState,
                };
                item.PropertyChanged += OnReturnLinePropertyChanged;
                ReturnLines.Add(item);
            }
        }

        BillLoaded = true;
        RecalculateTotals();
    }

    private void OnReturnLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SaleReturnLineItem.ReturnQty)
            or nameof(SaleReturnLineItem.IsSelected)
            or nameof(SaleReturnLineItem.TaxAmount))
        {
            RecalculateTotals();
        }
    }

    private void RecalculateTotals()
    {
        var selected = ReturnLines.Where(l => l.IsSelected && l.ReturnQty > 0).ToList();
        var sub = selected.Sum(l => l.ReturnAmount);
        var cgst = selected.Sum(l => l.CgstAmount);
        var sgst = selected.Sum(l => l.SgstAmount);
        var igst = selected.Sum(l => l.IgstAmount);
        var total = sub + cgst + sgst + igst;

        SubTotalFormatted = FormatRupee(sub);
        CgstTotalFormatted = FormatRupee(cgst);
        SgstTotalFormatted = FormatRupee(sgst);
        IgstTotalFormatted = FormatRupee(igst);
        ReturnTotalFormatted = FormatRupee(total);
    }

    [RelayCommand]
    private async Task PostReturn()
    {
        var selected = ReturnLines.Where(l => l.IsSelected && l.ReturnQty > 0).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Select at least one item with a return quantity.", "Sale Return", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var storeId = _services.StoreContext.StoreId;
            var deviceId = _services.StoreContext.DeviceId;
            var createdAt = DateTime.UtcNow.ToString("O");
            var eventId = Guid.NewGuid().ToString();

            var linesArr = new BsonArray();
            foreach (var l in selected)
            {
                linesArr.Add(new BsonDocument
                {
                    { "lineNo", l.LineNo },
                    { "sku", l.ProductCode },
                    { "description", l.Description },
                    { "returnQty", (double)l.ReturnQty },
                    { "rate", (double)l.Rate },
                    { "amount", (double)l.ReturnAmount },
                    { "taxPercent", (double)l.TaxPercent },
                    { "cgstAmt", (double)l.CgstAmount },
                    { "sgstAmt", (double)l.SgstAmount },
                    { "igstAmt", (double)l.IgstAmount },
                });
            }

            var sub = selected.Sum(l => l.ReturnAmount);
            var cgst = selected.Sum(l => l.CgstAmount);
            var sgst = selected.Sum(l => l.SgstAmount);
            var igst = selected.Sum(l => l.IgstAmount);
            var total = sub + cgst + sgst + igst;

            var returnDoc = new BsonDocument
            {
                { "returnNo", ReturnNo },
                { "originalBillNo", OriginalBillNo.Trim() },
                { "storeId", storeId },
                { "returnMode", ReturnMode == ReturnMode.CreditNote ? "credit_note" : "cash_refund" },
                { "reason", Reason },
                { "isInterState", IsInterState },
                { "lines", linesArr },
                { "subTotal", (double)sub },
                { "cgstTotal", (double)cgst },
                { "sgstTotal", (double)sgst },
                { "igstTotal", (double)igst },
                { "returnTotal", (double)total },
                { "status", "posted" },
                { "createdAtUtc", createdAt },
            };

            var returnsColl = _services.LocalDb.GetCollection<BsonDocument>("store_sale_returns");
            await returnsColl.InsertOneAsync(returnDoc);

            var payload = new BsonDocument
            {
                { "returnNo", ReturnNo },
                { "originalBillNo", OriginalBillNo.Trim() },
                { "returnMode", ReturnMode == ReturnMode.CreditNote ? "credit_note" : "cash_refund" },
                { "reason", Reason },
                { "isInterState", IsInterState },
                { "lines", linesArr },
                { "subTotal", (double)sub },
                { "cgstTotal", (double)cgst },
                { "sgstTotal", (double)sgst },
                { "igstTotal", (double)igst },
                { "returnTotal", (double)total },
            };

            var outboxEvent = new BsonDocument
            {
                { "eventId", eventId },
                { "storeId", storeId },
                { "deviceId", deviceId },
                { "type", "SaleReturnCreated" },
                { "createdAt", createdAt },
                { "payload", payload },
                { "status", "pending" },
            };

            var outbox = _services.LocalDb.GetCollection<BsonDocument>("outbox_events");
            await outbox.InsertOneAsync(outboxEvent);

            MessageBox.Show(
                $"Return {ReturnNo} posted. Mode: {(ReturnMode == ReturnMode.CreditNote ? "Credit Note" : "Cash Refund")}.",
                "Sale Return", MessageBoxButton.OK, MessageBoxImage.Information);

            ClearForm();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not post return: {ex.Message}", "Sale Return", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ClearForm()
    {
        foreach (var line in ReturnLines)
            line.PropertyChanged -= OnReturnLinePropertyChanged;

        OriginalBillNo = "";
        Reason = "";
        ReturnMode = ReturnMode.CreditNote;
        IsInterState = false;
        ReturnLines.Clear();
        BillLoaded = false;
        _originalBillDoc = null;
        AssignReturnNo();
        RecalculateTotals();
    }

    private static string FormatRupee(decimal value) => "₹ " + value.ToString("N2", InCulture);
}
