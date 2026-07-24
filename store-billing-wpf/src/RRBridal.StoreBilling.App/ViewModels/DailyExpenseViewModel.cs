using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Store;

namespace RRBridal.StoreBilling.App.ViewModels;

public sealed class DailyExpenseRow
{
    public required string ExpenseNo { get; init; }
    public required string Description { get; init; }
    public required string AmountDisplay { get; init; }
    public required string PostedAtLocal { get; init; }
    public required string BusinessDate { get; init; }
}

public partial class DailyExpenseViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    private readonly AppServices _services;
    private readonly IMongoCollection<BsonDocument> _expenses;

    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _amountText = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _dayTotalSummary = "₹ 0.00";
    [ObservableProperty] private string _storeDisplayName = "";
    [ObservableProperty] private string _tillDisplayLine = "";

    public ObservableCollection<DailyExpenseRow> Expenses { get; } = new();

    public DailyExpenseViewModel(AppServices services)
    {
        _services = services;
        _expenses = services.LocalDb.GetCollection<BsonDocument>("store_daily_expenses");
        ApplyBrandingFromShell();
        _ = RefreshCommand.ExecuteAsync(null);
    }

    public void ApplyBrandingFromShell()
    {
        var snap = _services.ShellBranding.Current;
        StoreDisplayName = snap.StoreDisplayName;
        TillDisplayLine = snap.TillDisplayLine;
    }

    [RelayCommand]
    private async Task Refresh()
    {
        StatusMessage = "Loading expenses…";
        try
        {
            var storeId = _services.StoreContext.StoreId;
            var businessDate = SelectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("storeId", storeId),
                Builders<BsonDocument>.Filter.Eq("businessDate", businessDate));

            var docs = await _expenses.Find(filter).ToListAsync();
            docs.Sort((a, b) =>
            {
                var aUtc = a.GetValue("createdAtUtc", "").AsString;
                var bUtc = b.GetValue("createdAtUtc", "").AsString;
                return string.Compare(bUtc, aUtc, StringComparison.Ordinal);
            });

            Expenses.Clear();
            decimal dayTotal = 0m;
            foreach (var doc in docs)
            {
                var amount = ReadDecimal(doc, "amount");
                dayTotal += amount;
                Expenses.Add(new DailyExpenseRow
                {
                    ExpenseNo = doc.GetValue("expenseNo", "").AsString,
                    Description = doc.GetValue("description", "").AsString,
                    AmountDisplay = MoneyMath.FormatRupee(amount),
                    PostedAtLocal = FormatPostedLocal(doc.GetValue("createdAtUtc", "").AsString),
                    BusinessDate = doc.GetValue("businessDate", "").AsString,
                });
            }

            DayTotalSummary = MoneyMath.FormatRupee(dayTotal);
            StatusMessage = Expenses.Count == 0
                ? $"No expenses for {SelectedDate:dd-MMM-yyyy}."
                : $"{Expenses.Count} expense(s) — total {DayTotalSummary}.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Load failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task PostExpense()
    {
        var description = (Description ?? "").Trim();
        if (string.IsNullOrEmpty(description))
        {
            AppDialog.Show("Enter a description for the expense.", "Daily Expense", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!decimal.TryParse((AmountText ?? "").Trim(), NumberStyles.Number, InCulture, out var amount) &&
            !decimal.TryParse((AmountText ?? "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            AppDialog.Show("Enter a valid amount.", "Daily Expense", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (amount <= 0)
        {
            AppDialog.Show("Amount must be greater than zero.", "Daily Expense", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var businessDate = SelectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var dayGuard = new DaySessionGuard(_services.DaySessions);
        var dayBlock = await dayGuard.ValidatePostingAsync(
            _services.StoreContext.StoreId,
            businessDate,
            _services.StoreContext.PosCounter);
        if (dayBlock != null)
        {
            AppDialog.Show(dayBlock, "Daily Expense", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var storeId = _services.StoreContext.StoreId;
            var deviceId = _services.StoreContext.DeviceId;
            var posCounter = _services.StoreContext.PosCounter;
            var expenseNo = await _services.BillNumberGenerator.NextDailyExpenseAsync();
            var createdAt = DateTime.UtcNow.ToString("O");

            var doc = new BsonDocument
            {
                { "expenseNo", expenseNo },
                { "storeId", storeId },
                { "deviceId", deviceId },
                { "posCounter", posCounter },
                { "businessDate", businessDate },
                { "description", description },
                { "amount", (double)amount },
                { "status", "posted" },
                { "createdAtUtc", createdAt },
            };

            await _expenses.InsertOneAsync(doc);
            await _services.BillingOutbox.PublishDailyExpenseCreatedAsync(doc);

            Description = "";
            AmountText = "";
            StatusMessage = $"Posted {expenseNo} — {MoneyMath.FormatRupee(amount)}.";
            await RefreshCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            AppDialog.Show("Post failed: " + ex.Message, "Daily Expense", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    partial void OnSelectedDateChanged(DateTime value) => _ = RefreshCommand.ExecuteAsync(null);

    public void ClearEntryForm()
    {
        Description = "";
        AmountText = "";
        StatusMessage = "Enter a new expense.";
    }

    private static decimal ReadDecimal(BsonDocument doc, string field)
    {
        if (!doc.TryGetValue(field, out var val) || val.IsBsonNull) return 0m;
        return val.BsonType switch
        {
            BsonType.Double => (decimal)val.AsDouble,
            BsonType.Int32 => val.AsInt32,
            BsonType.Int64 => val.AsInt64,
            BsonType.Decimal128 => (decimal)val.AsDecimal128,
            _ => decimal.TryParse(val.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m,
        };
    }

    private static string FormatPostedLocal(string createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(createdAtUtc)) return "—";
        if (!DateTime.TryParse(createdAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var utc))
            return createdAtUtc;
        return utc.ToLocalTime().ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture);
    }
}
