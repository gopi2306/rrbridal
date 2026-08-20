using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Audit;
using RRBridal.StoreBilling.App.Services.Auth;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Expenses;

public static class ExpenseGstMode
{
    public const string None = "none";
    public const string Inclusive = "inclusive";
    public const string Exclusive = "exclusive";
}

public static class ExpenseSupplyType
{
    public const string IntraState = "intra_state";
    public const string InterState = "inter_state";
}

public static class ExpensePaymentMode
{
    public const string Cash = "Cash";
    public const string Card = "Card";
    public const string Upi = "UPI";
    public const string BankTransfer = "Bank Transfer";

    public static readonly IReadOnlyList<string> All = [Cash, Card, Upi, BankTransfer];
}

public sealed record DailyExpenseTaxBreakdown(
    decimal TaxableAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal TotalAmount);

public sealed record DailyExpensePaymentLeg(string Mode, decimal Amount, string Reference = "");

public sealed class DailyExpenseDraft
{
    public string BusinessDate { get; init; } = "";
    public string SupplierName { get; init; } = "";
    public string SupplierGstin { get; init; } = "";
    public string SupplierStateCode { get; init; } = "";
    public string StoreStateCode { get; init; } = "";
    public string SupplierInvoiceNo { get; init; } = "";
    public string SupplierInvoiceDate { get; init; } = "";
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    public decimal EnteredAmount { get; init; }
    public string GstMode { get; init; } = ExpenseGstMode.None;
    public decimal GstRate { get; init; }
    public string SupplyType { get; init; } = ExpenseSupplyType.IntraState;
    public IReadOnlyList<DailyExpensePaymentLeg> Payments { get; init; } = Array.Empty<DailyExpensePaymentLeg>();
    public string LinkedDispatchNo { get; init; } = "";
    public string LinkedBillNo { get; init; } = "";
}

/// <summary>Pure, backward-compatible GST and tender helpers for expense documents.</summary>
public static class DailyExpenseDomain
{
    private static readonly Regex GstinPattern = new(
        "^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][0-9A-Z]Z[0-9A-Z]$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static DailyExpenseTaxBreakdown CalculateTax(
        decimal enteredAmount,
        string? gstMode,
        decimal gstRate,
        string? supplyType)
    {
        if (enteredAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(enteredAmount), "Amount must be greater than zero.");
        if (gstRate < 0 || gstRate > 100)
            throw new ArgumentOutOfRangeException(nameof(gstRate), "GST rate must be between 0 and 100.");

        var mode = (gstMode ?? ExpenseGstMode.None).Trim().ToLowerInvariant();
        var rate = mode == ExpenseGstMode.None ? 0m : gstRate;
        decimal taxable;
        decimal tax;
        decimal total;
        if (mode == ExpenseGstMode.Inclusive && rate > 0)
        {
            taxable = Round(enteredAmount * 100m / (100m + rate));
            total = Round(enteredAmount);
            tax = total - taxable;
        }
        else if (mode == ExpenseGstMode.Exclusive && rate > 0)
        {
            taxable = Round(enteredAmount);
            tax = Round(taxable * rate / 100m);
            total = taxable + tax;
        }
        else
        {
            taxable = Round(enteredAmount);
            tax = 0m;
            total = taxable;
        }

        if (string.Equals(supplyType, ExpenseSupplyType.InterState, StringComparison.OrdinalIgnoreCase))
            return new DailyExpenseTaxBreakdown(taxable, 0m, 0m, tax, total);

        var cgst = Round(tax / 2m);
        return new DailyExpenseTaxBreakdown(taxable, cgst, tax - cgst, 0m, total);
    }

    public static string? Validate(DailyExpenseDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.BusinessDate)) return "Business date is required.";
        if (string.IsNullOrWhiteSpace(draft.Description)) return "Description is required.";
        if (draft.EnteredAmount <= 0) return "Amount must be greater than zero.";
        if (draft.GstRate < 0 || draft.GstRate > 100) return "GST rate must be between 0 and 100.";
        var gstin = draft.SupplierGstin.Trim().ToUpperInvariant();
        var supplierStateCode = draft.SupplierStateCode.Trim();
        var storeStateCode = draft.StoreStateCode.Trim();
        var hasGst = !string.Equals(draft.GstMode, ExpenseGstMode.None, StringComparison.OrdinalIgnoreCase)
                     && draft.GstRate > 0;
        if (hasGst && string.IsNullOrWhiteSpace(draft.SupplierName)) return "Supplier name is required for a GST expense.";
        if (!string.IsNullOrWhiteSpace(draft.SupplierInvoiceDate)
            && !DateOnly.TryParseExact(draft.SupplierInvoiceDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return "Supplier invoice date must be valid.";
        if (gstin.Length > 0 && !GstinPattern.IsMatch(gstin)) return "Enter a valid 15-character supplier GSTIN.";
        if (supplierStateCode.Length > 0 && (supplierStateCode.Length != 2 || !supplierStateCode.All(char.IsDigit)))
            return "Supplier state code must contain two digits.";
        if (gstin.Length > 0 && supplierStateCode.Length > 0 && !gstin.StartsWith(supplierStateCode, StringComparison.Ordinal))
            return "Supplier GSTIN must match the supplier state code.";
        if (supplierStateCode.Length > 0 && storeStateCode.Length > 0)
        {
            var expectedSupplyType = string.Equals(supplierStateCode, storeStateCode, StringComparison.Ordinal)
                ? ExpenseSupplyType.IntraState
                : ExpenseSupplyType.InterState;
            if (!string.Equals(draft.SupplyType, expectedSupplyType, StringComparison.OrdinalIgnoreCase))
                return "Supply type does not match the supplier and store GST state codes.";
        }
        var total = CalculateTax(draft.EnteredAmount, draft.GstMode, draft.GstRate, draft.SupplyType).TotalAmount;
        var legs = draft.Payments.Where(p => p.Amount > 0).ToList();
        if (legs.Count == 0) return "Enter at least one payment amount.";
        if (legs.Any(p => !ExpensePaymentMode.All.Contains(p.Mode, StringComparer.OrdinalIgnoreCase)))
            return "A payment leg has an unsupported mode.";
        if (Math.Abs(legs.Sum(p => p.Amount) - total) > 0.01m)
            return $"Payment legs must equal the expense total ({total:0.00}).";
        return null;
    }

    public static decimal CashAmount(BsonDocument doc)
    {
        if (doc.TryGetValue("payments", out var paymentsValue) && paymentsValue.IsBsonArray)
        {
            var legs = paymentsValue.AsBsonArray.OfType<BsonDocument>().ToList();
            if (legs.Count > 0)
            {
                return legs
                    .Where(p => string.Equals(ReadString(p, "mode") ?? ReadString(p, "provider"), ExpensePaymentMode.Cash, StringComparison.OrdinalIgnoreCase))
                    .Sum(p => ReadDecimal(p, "amount"));
            }
        }

        // Legacy expense documents predate tender legs and were always cash expenses.
        return ReadDecimal(doc, "amount");
    }

    public static BsonDocument BuildDocument(
        string expenseNo,
        string storeId,
        string deviceId,
        string posCounter,
        DailyExpenseDraft draft,
        string createdAtUtc,
        BsonDocument? existing = null)
    {
        var tax = CalculateTax(draft.EnteredAmount, draft.GstMode, draft.GstRate, draft.SupplyType);
        var doc = existing == null ? new BsonDocument() : (BsonDocument)existing.DeepClone();
        doc["expenseNo"] = expenseNo;
        doc["storeId"] = storeId;
        doc["deviceId"] = deviceId;
        doc["posCounter"] = posCounter;
        doc["businessDate"] = draft.BusinessDate;
        doc["supplierName"] = draft.SupplierName.Trim();
        doc["supplierGstin"] = draft.SupplierGstin.Trim().ToUpperInvariant();
        doc["supplierStateCode"] = draft.SupplierStateCode.Trim();
        doc["storeStateCode"] = draft.StoreStateCode.Trim();
        doc["supplierInvoiceNo"] = draft.SupplierInvoiceNo.Trim();
        doc["supplierInvoiceDate"] = draft.SupplierInvoiceDate;
        doc["category"] = draft.Category.Trim();
        doc["description"] = draft.Description.Trim();
        doc["enteredAmount"] = (double)Round(draft.EnteredAmount);
        doc["gstMode"] = draft.GstMode;
        doc["gstRate"] = (double)draft.GstRate;
        doc["supplyType"] = draft.SupplyType;
        doc["taxableAmount"] = (double)tax.TaxableAmount;
        doc["cgstAmount"] = (double)tax.CgstAmount;
        doc["sgstAmount"] = (double)tax.SgstAmount;
        doc["igstAmount"] = (double)tax.IgstAmount;
        doc["amount"] = (double)tax.TotalAmount; // Existing readers continue to use amount.
        doc["payments"] = new BsonArray(draft.Payments
            .Where(p => p.Amount > 0)
            .Select(p => new BsonDocument
            {
                { "mode", p.Mode },
                { "amount", (double)Round(p.Amount) },
                { "reference", p.Reference.Trim() },
            }));
        doc["status"] = "posted";
        if (!string.IsNullOrWhiteSpace(draft.LinkedDispatchNo))
            doc["dispatchNo"] = draft.LinkedDispatchNo.Trim();
        if (!string.IsNullOrWhiteSpace(draft.LinkedBillNo))
            doc["billNo"] = draft.LinkedBillNo.Trim();
        if (!doc.Contains("createdAtUtc"))
            doc["createdAtUtc"] = createdAtUtc;
        return doc;
    }

    public static decimal ReadDecimal(BsonDocument doc, string field)
    {
        if (!doc.TryGetValue(field, out var value) || value.IsBsonNull) return 0m;
        return value.BsonType switch
        {
            BsonType.Double => (decimal)value.AsDouble,
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            BsonType.Decimal128 => (decimal)value.AsDecimal128,
            _ => decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m,
        };
    }

    public static string? ReadString(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var value) && !value.IsBsonNull ? (value.IsString ? value.AsString : value.ToString()) : null;

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed class DailyExpenseService
{
    private readonly IMongoCollection<BsonDocument> _expenses;
    private readonly StoreContext _store;
    private readonly BillNumberGenerator _numbers;
    private readonly BillingOutboxPublisher _outbox;
    private readonly StoreAuditLogService _audit;

    public DailyExpenseService(
        IMongoDatabase db,
        StoreContext store,
        BillNumberGenerator numbers,
        BillingOutboxPublisher outbox,
        StoreAuditLogService audit)
    {
        _expenses = db.GetCollection<BsonDocument>("store_daily_expenses");
        _store = store;
        _numbers = numbers;
        _outbox = outbox;
        _audit = audit;
    }

    public async Task<BsonDocument> CreateAsync(DailyExpenseDraft draft, UserSession? session, bool persistLocally, CancellationToken ct = default)
    {
        var error = DailyExpenseDomain.Validate(draft);
        if (error != null) throw new InvalidOperationException(error);
        var expenseNo = await _numbers.NextDailyExpenseAsync();
        var now = DateTime.UtcNow.ToString("O");
        var doc = DailyExpenseDomain.BuildDocument(expenseNo, _store.StoreId, _store.DeviceId, _store.PosCounter, draft, now);
        doc["revision"] = 1;
        AppendAudit(doc, "created", session, now);
        if (persistLocally)
            await _expenses.InsertOneAsync(doc, cancellationToken: ct);
        await _outbox.PublishDailyExpenseCreatedAsync(doc, ct);
        await LogAsync(doc, "created", session, ct);
        return doc;
    }

    public async Task<BsonDocument> UpdateAsync(BsonDocument existing, DailyExpenseDraft draft, UserSession? session, bool persistLocally, CancellationToken ct = default)
    {
        if (!string.Equals(DailyExpenseDomain.ReadString(existing, "status") ?? "posted", "posted", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Voided expenses cannot be edited.");
        var error = DailyExpenseDomain.Validate(draft);
        if (error != null) throw new InvalidOperationException(error);
        var expenseNo = DailyExpenseDomain.ReadString(existing, "expenseNo") ?? throw new InvalidOperationException("Expense number is missing.");
        var now = DateTime.UtcNow.ToString("O");
        var doc = DailyExpenseDomain.BuildDocument(expenseNo, _store.StoreId, _store.DeviceId, _store.PosCounter, draft, now, existing);
        doc["revision"] = Math.Max(1, (int)DailyExpenseDomain.ReadDecimal(existing, "revision")) + 1;
        doc["updatedAtUtc"] = now;
        AppendAudit(doc, "updated", session, now);
        if (persistLocally)
            await ReplaceLocalAsync(expenseNo, doc, ct);
        await _outbox.PublishDailyExpenseUpdatedAsync(doc, ct);
        await LogAsync(doc, "updated", session, ct);
        return doc;
    }

    public async Task<BsonDocument> VoidAsync(BsonDocument existing, string reason, UserSession? session, bool persistLocally, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Void reason is required.");
        if (string.Equals(DailyExpenseDomain.ReadString(existing, "status"), "void", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Expense is already voided.");
        var doc = (BsonDocument)existing.DeepClone();
        var expenseNo = DailyExpenseDomain.ReadString(doc, "expenseNo") ?? throw new InvalidOperationException("Expense number is missing.");
        var now = DateTime.UtcNow.ToString("O");
        doc["status"] = "void";
        doc["voidReason"] = reason.Trim();
        doc["voidedAtUtc"] = now;
        doc["revision"] = Math.Max(1, (int)DailyExpenseDomain.ReadDecimal(existing, "revision")) + 1;
        AppendAudit(doc, "voided", session, now);
        if (persistLocally)
            await ReplaceLocalAsync(expenseNo, doc, ct);
        await _outbox.PublishDailyExpenseVoidedAsync(doc, ct);
        await LogAsync(doc, "voided", session, ct);
        return doc;
    }

    private Task ReplaceLocalAsync(string expenseNo, BsonDocument doc, CancellationToken ct) =>
        _expenses.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
                Builders<BsonDocument>.Filter.Eq("expenseNo", expenseNo)),
            doc,
            new ReplaceOptions { IsUpsert = false },
            ct);

    private async Task LogAsync(BsonDocument doc, string action, UserSession? session, CancellationToken ct)
    {
        var (name, email) = StoreAuditLogService.ActorFromSession(session);
        await _audit.LogEventAsync(new StoreAuditEvent
        {
            EntityType = "daily_expense",
            EntityId = DailyExpenseDomain.ReadString(doc, "expenseNo") ?? "",
            Action = action,
            ActorName = name,
            ActorEmail = email,
            Metadata = new BsonDocument
            {
                { "revision", DailyExpenseDomain.ReadDecimal(doc, "revision") },
                { "amount", DailyExpenseDomain.ReadDecimal(doc, "amount") },
                { "status", DailyExpenseDomain.ReadString(doc, "status") ?? "" },
            },
        }, ct);
    }

    private static void AppendAudit(BsonDocument doc, string action, UserSession? session, string atUtc)
    {
        var (name, email) = StoreAuditLogService.ActorFromSession(session);
        var history = doc.TryGetValue("auditHistory", out var value) && value.IsBsonArray
            ? value.AsBsonArray
            : new BsonArray();
        history.Add(new BsonDocument
        {
            { "action", action },
            { "atUtc", atUtc },
            { "actorName", name ?? "" },
            { "actorEmail", email ?? "" },
        });
        doc["auditHistory"] = history;
    }
}
