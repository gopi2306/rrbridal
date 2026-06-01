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
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class AdjustmentBillViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    private readonly AppServices _services;

    [ObservableProperty] private string _originalBillNo = "";
    [ObservableProperty] private bool _billLoaded;
    [ObservableProperty] private string _reason = "";
    [ObservableProperty] private string _adjustmentNo = "";
    [ObservableProperty] private bool _isInterState;

    [ObservableProperty] private string _diffSubTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _diffCgstFormatted = "₹ 0.00";
    [ObservableProperty] private string _diffSgstFormatted = "₹ 0.00";
    [ObservableProperty] private string _diffIgstFormatted = "₹ 0.00";
    [ObservableProperty] private string _diffPayableFormatted = "₹ 0.00";

    [ObservableProperty] private string _originalPayableFormatted = "₹ 0.00";
    [ObservableProperty] private string _adjustedPayableFormatted = "₹ 0.00";

    public ObservableCollection<AdjustmentLineItem> AdjustmentLines { get; } = new();

    private BsonDocument? _originalBillDoc;

    public AdjustmentBillViewModel(AppServices services)
    {
        _services = services;
        _ = AssignAdjustmentNoAsync();
    }

    private async Task AssignAdjustmentNoAsync()
    {
        AdjustmentNo = await _services.BillNumberGenerator.NextAdjustmentAsync();
    }

    [RelayCommand]
    private async Task LookupBill()
    {
        var billNo = (OriginalBillNo ?? "").Trim();
        if (string.IsNullOrEmpty(billNo))
        {
            MessageBox.Show("Enter a bill number to look up.", "Adjustment Bill", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var coll = _services.LocalDb.GetCollection<BsonDocument>("store_bills");
        var doc = await coll.Find(new BsonDocument("billNo", billNo)).FirstOrDefaultAsync();
        if (doc == null)
        {
            MessageBox.Show($"Bill '{billNo}' not found.", "Adjustment Bill", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _originalBillDoc = doc;
        IsInterState = doc.Contains("isInterState") && doc["isInterState"].AsBoolean;

        foreach (var line in AdjustmentLines)
            line.PropertyChanged -= OnAdjLinePropertyChanged;
        AdjustmentLines.Clear();

        if (doc.Contains("lines") && doc["lines"].IsBsonArray)
        {
            foreach (BsonDocument lineBson in doc["lines"].AsBsonArray.OfType<BsonDocument>())
            {
                var qty = (decimal)lineBson.GetValue("qty", 0).ToDouble();
                var rate = (decimal)lineBson.GetValue("rate", 0).ToDouble();
                var item = new AdjustmentLineItem
                {
                    LineNo = lineBson.GetValue("lineNo", 0).ToInt32(),
                    ProductCode = lineBson.GetValue("sku", "").AsString,
                    Description = lineBson.GetValue("description", "").AsString,
                    OriginalQty = qty,
                    OriginalRate = rate,
                    OriginalAmount = MoneyMath.RoundAmount(qty * rate),
                    TaxPercent = (decimal)lineBson.GetValue("taxPercent", 0).ToDouble(),
                    IsIgst = IsInterState,
                    AdjustedQty = qty,
                    AdjustedRate = rate,
                };
                item.PropertyChanged += OnAdjLinePropertyChanged;
                AdjustmentLines.Add(item);
            }
        }

        var origPayable = doc.Contains("payable") ? (decimal)doc["payable"].ToDouble() : 0m;
        OriginalPayableFormatted = MoneyMath.FormatPayable(origPayable);

        BillLoaded = true;
        RecalculateTotals();
    }

    private void OnAdjLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AdjustmentLineItem.AdjustedAmount)
            or nameof(AdjustmentLineItem.DiffAmount)
            or nameof(AdjustmentLineItem.DiffTax))
        {
            RecalculateTotals();
        }
    }

    private void RecalculateTotals()
    {
        var diffSub = AdjustmentLines.Sum(l => l.DiffAmount);
        var diffCgst = AdjustmentLines.Sum(l => l.DiffCgst);
        var diffSgst = AdjustmentLines.Sum(l => l.DiffSgst);
        var diffIgst = AdjustmentLines.Sum(l => l.DiffIgst);
        var diffPayable = diffSub + diffCgst + diffSgst + diffIgst;

        var adjSub = AdjustmentLines.Sum(l => l.AdjustedAmount);
        var adjTax = AdjustmentLines.Sum(l =>
        {
            var amt = l.AdjustedAmount;
            if (l.IsIgst)
                return MoneyMath.RoundAmount(amt * l.TaxPercent / 100m);
            return MoneyMath.RoundAmount(amt * (l.TaxPercent / 2m) / 100m) * 2;
        });

        DiffSubTotalFormatted = MoneyMath.FormatRupee(diffSub);
        DiffCgstFormatted = MoneyMath.FormatRupee(diffCgst);
        DiffSgstFormatted = MoneyMath.FormatRupee(diffSgst);
        DiffIgstFormatted = MoneyMath.FormatRupee(diffIgst);
        DiffPayableFormatted = MoneyMath.FormatRupee(diffPayable);
        AdjustedPayableFormatted = MoneyMath.FormatPayable(adjSub + adjTax);
    }

    [RelayCommand]
    private async Task PostAdjustment()
    {
        if (AdjustmentLines.Count == 0)
        {
            MessageBox.Show("Load a bill first.", "Adjustment Bill", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var storeId = _services.StoreContext.StoreId;
            var deviceId = _services.StoreContext.DeviceId;
            var posCounter = _services.StoreContext.PosCounter;
            var createdAt = DateTime.UtcNow.ToString("O");
            var eventId = Guid.NewGuid().ToString();

            var linesArr = new BsonArray();
            foreach (var l in AdjustmentLines)
            {
                linesArr.Add(new BsonDocument
                {
                    { "lineNo", l.LineNo },
                    { "sku", l.ProductCode },
                    { "description", l.Description },
                    { "originalQty", (double)l.OriginalQty },
                    { "originalRate", (double)l.OriginalRate },
                    { "originalAmount", (double)l.OriginalAmount },
                    { "adjustedQty", (double)l.AdjustedQty },
                    { "adjustedRate", (double)l.AdjustedRate },
                    { "adjustedAmount", (double)l.AdjustedAmount },
                    { "diffAmount", (double)l.DiffAmount },
                    { "taxPercent", (double)l.TaxPercent },
                    { "diffCgst", (double)l.DiffCgst },
                    { "diffSgst", (double)l.DiffSgst },
                    { "diffIgst", (double)l.DiffIgst },
                });
            }

            var origPayable = _originalBillDoc?.Contains("payable") == true
                ? _originalBillDoc["payable"].ToDouble()
                : 0.0;

            var adjSub = (double)AdjustmentLines.Sum(l => l.AdjustedAmount);
            var adjTax = (double)AdjustmentLines.Sum(l =>
            {
                var amt = l.AdjustedAmount;
                if (l.IsIgst) return MoneyMath.RoundAmount(amt * l.TaxPercent / 100m);
                return MoneyMath.RoundAmount(amt * (l.TaxPercent / 2m) / 100m) * 2;
            });
            var adjustedPayable = adjSub + adjTax;
            var diffPayable = adjustedPayable - origPayable;

            var adjDoc = new BsonDocument
            {
                { "adjustmentNo", AdjustmentNo },
                { "originalBillNo", OriginalBillNo.Trim() },
                { "storeId", storeId },
                { "deviceId", deviceId },
                { "posCounter", posCounter },
                { "isInterState", IsInterState },
                { "reason", Reason },
                { "lines", linesArr },
                { "originalPayable", origPayable },
                { "adjustedPayable", adjustedPayable },
                { "diffPayable", diffPayable },
                { "status", "posted" },
                { "createdAtUtc", createdAt },
            };

            var adjColl = _services.LocalDb.GetCollection<BsonDocument>("store_adjustments");
            await adjColl.InsertOneAsync(adjDoc);

            var payload = new BsonDocument
            {
                { "adjustmentNo", AdjustmentNo },
                { "originalBillNo", OriginalBillNo.Trim() },
                { "isInterState", IsInterState },
                { "reason", Reason },
                { "lines", linesArr },
                { "originalPayable", origPayable },
                { "adjustedPayable", adjustedPayable },
                { "diffPayable", diffPayable },
            };

            var hash = JsonSerializer.Serialize(new
            {
                adjustmentNo = AdjustmentNo,
                originalBillNo = OriginalBillNo.Trim(),
                diffPayable,
            });

            var outboxEvent = new BsonDocument
            {
                { "eventId", eventId },
                { "storeId", storeId },
                { "deviceId", deviceId },
                { "type", "AdjustmentBillCreated" },
                { "createdAt", createdAt },
                { "payload", payload },
                { "hash", hash },
                { "status", "pending" },
            };

            var outbox = _services.LocalDb.GetCollection<BsonDocument>("outbox_events");
            await outbox.InsertOneAsync(outboxEvent);

            MessageBox.Show(
                $"Adjustment {AdjustmentNo} posted.",
                "Adjustment Bill", MessageBoxButton.OK, MessageBoxImage.Information);

            ClearForm();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not post adjustment: {ex.Message}", "Adjustment Bill", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ClearForm()
    {
        foreach (var line in AdjustmentLines)
            line.PropertyChanged -= OnAdjLinePropertyChanged;

        OriginalBillNo = "";
        Reason = "";
        IsInterState = false;
        AdjustmentLines.Clear();
        BillLoaded = false;
        _originalBillDoc = null;
        OriginalPayableFormatted = MoneyMath.FormatPayable(0);
        await AssignAdjustmentNoAsync();
        RecalculateTotals();
    }
}
