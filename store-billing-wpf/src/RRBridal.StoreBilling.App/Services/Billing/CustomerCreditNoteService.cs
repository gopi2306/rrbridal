using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Customers;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Billing;

public sealed class CreditNoteSearchRow
{
    public required string CreditNoteNo { get; init; }
    public required string ReturnNo { get; init; }
    public required string OriginalBillNo { get; init; }
    public string CustomerName { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public decimal Amount { get; init; }
    public decimal RemainingAmount { get; init; }
    public string Status { get; init; } = "";
    public string CreatedAtDisplay { get; init; } = "";
    public DateTime SortUtc { get; init; }
}

public sealed class CustomerCreditNoteService
{
    public const string StatusAvailable = "available";
    public const string StatusConsumed = "consumed";

    private readonly IMongoCollection<BsonDocument> _notes;
    private readonly IMongoCollection<BsonDocument> _cashouts;
    private readonly BillingOutboxPublisher? _outbox;

    public CustomerCreditNoteService(IMongoDatabase localDb, BillingOutboxPublisher? outbox = null)
    {
        _notes = localDb.GetCollection<BsonDocument>("customer_credit_notes");
        _cashouts = localDb.GetCollection<BsonDocument>("store_credit_note_cashouts");
        _outbox = outbox;
    }

    public async Task<CustomerCreditNoteRecord?> FindByOriginalBillAsync(
        string storeId,
        string originalBillNo,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(originalBillNo))
            return null;

        var doc = await _notes.Find(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("storeId", storeId?.Trim() ?? ""),
                Builders<BsonDocument>.Filter.Eq("originalBillNo", originalBillNo.Trim()),
                Builders<BsonDocument>.Filter.Ne("isLegacy", true)))
            .FirstOrDefaultAsync(ct);
        return doc == null ? null : Map(doc);
    }

    public async Task<string?> CreateFromReturnAsync(
        string returnNo,
        string originalBillNo,
        string customerCode,
        string customerName,
        string customerPhone,
        decimal creditBalance,
        string storeId,
        bool isLegacy = false,
        string? originalBillDate = null,
        CancellationToken ct = default)
    {
        if (creditBalance <= 0 || string.IsNullOrWhiteSpace(returnNo))
            return null;

        var phoneNorm = PhoneMatchHelper.NormalizePhone(customerPhone);
        if (string.IsNullOrEmpty(phoneNorm) || phoneNorm.Length < 10)
            return null;

        var billNo = originalBillNo?.Trim() ?? "";
        if (!isLegacy && !string.IsNullOrEmpty(billNo))
        {
            var existingForBill = await FindByOriginalBillAsync(storeId, billNo, ct);
            if (existingForBill != null)
            {
                if (string.Equals(existingForBill.ReturnNo, returnNo.Trim(), StringComparison.OrdinalIgnoreCase))
                    return existingForBill.CreditNoteNo;
                return null;
            }
        }

        var creditNoteNo = returnNo.StartsWith("CN-", StringComparison.OrdinalIgnoreCase)
            ? returnNo.Trim()
            : $"CN-{returnNo.Trim()}";

        var existing = await _notes.Find(
            Builders<BsonDocument>.Filter.Eq("creditNoteNo", creditNoteNo)).FirstOrDefaultAsync(ct);
        if (existing != null)
            return creditNoteNo;

        var doc = new BsonDocument
        {
            { "creditNoteNo", creditNoteNo },
            { "returnNo", returnNo.Trim() },
            { "originalBillNo", originalBillNo?.Trim() ?? "" },
            { "customerCode", customerCode?.Trim() ?? "" },
            { "customerName", customerName?.Trim() ?? "" },
            { "customerPhone", customerPhone?.Trim() ?? "" },
            { "customerPhoneNorm", phoneNorm },
            { "amount", (double)creditBalance },
            { "remainingAmount", (double)creditBalance },
            { "totalApplied", 0 },
            { "status", StatusAvailable },
            { "storeId", storeId?.Trim() ?? "" },
            { "createdAtUtc", DateTime.UtcNow.ToString("O") },
        };

        if (isLegacy)
        {
            doc["isLegacy"] = true;
            doc["source"] = "pre_system";
            if (!string.IsNullOrWhiteSpace(originalBillDate))
                doc["originalBillDate"] = originalBillDate.Trim();
        }

        await _notes.InsertOneAsync(doc, cancellationToken: ct);
        if (_outbox != null)
            await _outbox.PublishCreditNoteCreatedAsync(doc, ct);
        return creditNoteNo;
    }

    public async Task<string?> AddCreditFromReturnAsync(
        string creditNoteNo,
        string returnNo,
        decimal additionalCredit,
        string storeId,
        CancellationToken ct = default)
    {
        if (additionalCredit <= 0 || string.IsNullOrWhiteSpace(creditNoteNo))
            return null;

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId?.Trim() ?? ""),
            Builders<BsonDocument>.Filter.Eq("creditNoteNo", creditNoteNo.Trim()));

        var doc = await _notes.Find(filter).FirstOrDefaultAsync(ct);
        if (doc == null)
            return null;

        var newAmount = ReadDecimal(doc, "amount") + additionalCredit;
        var newRemaining = ReadDecimal(doc, "remainingAmount") + additionalCredit;

        await _notes.UpdateOneAsync(
            filter,
            Builders<BsonDocument>.Update
                .Set("amount", (double)newAmount)
                .Set("remainingAmount", (double)newRemaining)
                .Set("status", StatusAvailable),
            cancellationToken: ct);

        return creditNoteNo;
    }

    public async Task<IReadOnlyList<CustomerCreditNoteRecord>> ListAvailableForCustomerAsync(
        string storeId,
        string? customerCode,
        string? customerPhone,
        CancellationToken ct = default)
    {
        var phoneNorm = PhoneMatchHelper.NormalizePhone(customerPhone);
        if (string.IsNullOrEmpty(phoneNorm) && string.IsNullOrWhiteSpace(customerCode))
            return Array.Empty<CustomerCreditNoteRecord>();

        var filters = new List<FilterDefinition<BsonDocument>>
        {
            Builders<BsonDocument>.Filter.Eq("storeId", storeId?.Trim() ?? ""),
            Builders<BsonDocument>.Filter.Eq("status", StatusAvailable),
            Builders<BsonDocument>.Filter.Gt("remainingAmount", 0),
        };

        if (!string.IsNullOrEmpty(phoneNorm))
            filters.Add(Builders<BsonDocument>.Filter.Eq("customerPhoneNorm", phoneNorm));
        else if (!string.IsNullOrWhiteSpace(customerCode))
            filters.Add(Builders<BsonDocument>.Filter.Eq("customerCode", customerCode.Trim()));

        var filter = Builders<BsonDocument>.Filter.And(filters);
        var sort = Builders<BsonDocument>.Sort.Descending("createdAtUtc");
        var docs = await _notes.Find(filter).Sort(sort).Limit(50).ToListAsync(ct);
        return docs.Select(Map).Where(r => r.RemainingAmount > 0).ToList();
    }

    public async Task<CustomerCreditNoteRecord?> GetByCreditNoteNoAsync(
        string creditNoteNo,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(creditNoteNo))
            return null;

        var doc = await _notes.Find(
            Builders<BsonDocument>.Filter.Eq("creditNoteNo", creditNoteNo.Trim()))
            .FirstOrDefaultAsync(ct);
        return doc == null ? null : Map(doc);
    }

    public async Task<IReadOnlyList<CreditNoteSearchRow>> SearchCreditNotesAsync(
        string storeId,
        string? originalBillNo,
        string? returnNo,
        string? customerName,
        string? mobile,
        DateTime? dateFrom,
        DateTime? dateTo,
        int limit = 100,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var filters = new List<FilterDefinition<BsonDocument>>
        {
            Builders<BsonDocument>.Filter.Eq("storeId", storeId?.Trim() ?? ""),
        };

        if (!string.IsNullOrWhiteSpace(originalBillNo))
        {
            var safe = Regex.Escape(originalBillNo.Trim());
            filters.Add(Builders<BsonDocument>.Filter.Regex("originalBillNo", new BsonRegularExpression(safe, "i")));
        }

        if (!string.IsNullOrWhiteSpace(returnNo))
        {
            var safe = Regex.Escape(returnNo.Trim());
            filters.Add(Builders<BsonDocument>.Filter.Regex("returnNo", new BsonRegularExpression(safe, "i")));
        }

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            var safe = Regex.Escape(customerName.Trim());
            filters.Add(Builders<BsonDocument>.Filter.Regex("customerName", new BsonRegularExpression(safe, "i")));
        }

        var phoneNorm = PhoneMatchHelper.NormalizePhone(mobile);
        if (!string.IsNullOrEmpty(phoneNorm))
        {
            filters.Add(Builders<BsonDocument>.Filter.Regex(
                "customerPhoneNorm",
                new BsonRegularExpression($"^{Regex.Escape(phoneNorm)}", "i")));
        }

        var docs = await _notes
            .Find(Builders<BsonDocument>.Filter.And(filters))
            .Sort(Builders<BsonDocument>.Sort.Descending("createdAtUtc"))
            .Limit(limit * 3)
            .ToListAsync(ct);

        return docs
            .Select(MapSearchRow)
            .Where(r => r != null)
            .Cast<CreditNoteSearchRow>()
            .Where(r => InCreatedDateRange(r.SortUtc, dateFrom, dateTo))
            .Take(limit)
            .ToList();
    }

    public static bool InCreatedDateRange(DateTime utc, DateTime? from, DateTime? to)
    {
        if (utc == DateTime.MinValue)
            return from == null && to == null;
        if (from.HasValue && utc.Date < from.Value.Date)
            return false;
        if (to.HasValue && utc.Date > to.Value.Date)
            return false;
        return true;
    }

    public static bool MatchesMobileFilter(BsonDocument doc, string? mobile)
    {
        var phoneNorm = PhoneMatchHelper.NormalizePhone(mobile);
        if (string.IsNullOrEmpty(phoneNorm))
            return true;

        var stored = ReadString(doc, "customerPhoneNorm") ?? PhoneMatchHelper.NormalizePhone(ReadString(doc, "customerPhone"));
        return !string.IsNullOrEmpty(stored)
            && stored.StartsWith(phoneNorm, StringComparison.Ordinal);
    }

    public async Task<bool> ConsumeAsync(
        string creditNoteNo,
        string billNo,
        decimal amountApplied,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(creditNoteNo) || amountApplied <= 0)
            return false;

        var doc = await _notes.Find(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("creditNoteNo", creditNoteNo.Trim()),
                Builders<BsonDocument>.Filter.Eq("status", StatusAvailable)))
            .FirstOrDefaultAsync(ct);

        if (doc == null)
            return false;

        var remaining = ReadDecimal(doc, "remainingAmount");
        if (amountApplied > remaining)
            return false;

        var newRemaining = remaining - amountApplied;
        var totalApplied = ReadDecimal(doc, "totalApplied") + amountApplied;
        var bill = billNo?.Trim() ?? "";

        var updates = new List<UpdateDefinition<BsonDocument>>
        {
            Builders<BsonDocument>.Update.Set("totalApplied", (double)totalApplied),
            Builders<BsonDocument>.Update.Set("lastAppliedBillNo", bill),
            Builders<BsonDocument>.Update.Set("lastAmountApplied", (double)amountApplied),
        };

        if (newRemaining <= 0)
        {
            updates.Add(Builders<BsonDocument>.Update.Set("status", StatusConsumed));
            updates.Add(Builders<BsonDocument>.Update.Set("remainingAmount", 0));
            updates.Add(Builders<BsonDocument>.Update.Set("consumedBillNo", bill));
            updates.Add(Builders<BsonDocument>.Update.Set("consumedAtUtc", DateTime.UtcNow.ToString("O")));
        }
        else
        {
            updates.Add(Builders<BsonDocument>.Update.Set("remainingAmount", (double)newRemaining));
        }

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("creditNoteNo", creditNoteNo.Trim()),
            Builders<BsonDocument>.Filter.Eq("status", StatusAvailable),
            Builders<BsonDocument>.Filter.Gte("remainingAmount", (double)amountApplied));

        var result = await _notes.UpdateOneAsync(
            filter,
            Builders<BsonDocument>.Update.Combine(updates),
            cancellationToken: ct);
        if (result.ModifiedCount > 0 && _outbox != null)
        {
            var status = newRemaining <= 0 ? StatusConsumed : StatusAvailable;
            await _outbox.PublishCreditNoteAppliedAsync(
                creditNoteNo.Trim(),
                bill,
                amountApplied,
                newRemaining,
                status,
                ct);
        }
        return result.ModifiedCount > 0;
    }

    public async Task<bool> CashOutAsync(
        string creditNoteNo,
        decimal cashOutAmount,
        string billNo,
        string storeId,
        string posCounter,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(creditNoteNo) || cashOutAmount <= 0)
            return false;

        var doc = await _notes.Find(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("creditNoteNo", creditNoteNo.Trim()),
                Builders<BsonDocument>.Filter.Eq("status", StatusAvailable)))
            .FirstOrDefaultAsync(ct);

        if (doc == null)
            return false;

        var remainingBefore = ReadDecimal(doc, "remainingAmount");
        if (cashOutAmount > remainingBefore)
            return false;

        var remainingAfter = remainingBefore - cashOutAmount;
        var totalApplied = ReadDecimal(doc, "totalApplied");
        var bill = billNo?.Trim() ?? "";
        var cashoutNo = $"COUT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}";

        var updates = new List<UpdateDefinition<BsonDocument>>
        {
            Builders<BsonDocument>.Update.Set("remainingAmount", (double)remainingAfter),
            Builders<BsonDocument>.Update.Set("totalApplied", (double)(totalApplied + cashOutAmount)),
            Builders<BsonDocument>.Update.Set("lastAppliedBillNo", bill),
            Builders<BsonDocument>.Update.Set("lastAmountApplied", (double)cashOutAmount),
        };

        if (remainingAfter <= 0)
        {
            updates.Add(Builders<BsonDocument>.Update.Set("status", StatusConsumed));
            updates.Add(Builders<BsonDocument>.Update.Set("consumedBillNo", bill));
            updates.Add(Builders<BsonDocument>.Update.Set("consumedAtUtc", DateTime.UtcNow.ToString("O")));
        }

        var application = new BsonDocument
        {
            { "billNo", bill },
            { "amountApplied", (double)cashOutAmount },
            { "appliedAt", DateTime.UtcNow.ToString("O") },
            { "type", "cash_out" },
        };

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("creditNoteNo", creditNoteNo.Trim()),
            Builders<BsonDocument>.Filter.Eq("status", StatusAvailable),
            Builders<BsonDocument>.Filter.Gte("remainingAmount", (double)cashOutAmount));

        var result = await _notes.UpdateOneAsync(
            filter,
            Builders<BsonDocument>.Update.Combine(updates).Push("applications", application),
            cancellationToken: ct);

        if (result.ModifiedCount == 0)
            return false;

        var cashoutDoc = new BsonDocument
        {
            { "cashoutNo", cashoutNo },
            { "creditNoteNo", creditNoteNo.Trim() },
            { "billNo", bill },
            { "cashRefunded", (double)cashOutAmount },
            { "remainingBefore", (double)remainingBefore },
            { "remainingAfter", (double)remainingAfter },
            { "storeId", storeId?.Trim() ?? "" },
            { "posCounter", posCounter?.Trim() ?? "" },
            { "customerCode", ReadString(doc, "customerCode") ?? "" },
            { "customerName", ReadString(doc, "customerName") ?? "" },
            { "customerPhone", ReadString(doc, "customerPhone") ?? "" },
            { "status", "posted" },
            { "createdAtUtc", DateTime.UtcNow.ToString("O") },
        };

        await _cashouts.InsertOneAsync(cashoutDoc, cancellationToken: ct);

        if (_outbox != null)
        {
            await _outbox.PublishCreditNoteCashedOutAsync(
                cashoutDoc,
                creditNoteNo.Trim(),
                bill,
                cashOutAmount,
                remainingAfter,
                remainingAfter <= 0 ? StatusConsumed : StatusAvailable,
                ct);
        }

        return true;
    }

    private static CreditNoteSearchRow? MapSearchRow(BsonDocument doc)
    {
        var creditNoteNo = ReadString(doc, "creditNoteNo") ?? "";
        if (string.IsNullOrEmpty(creditNoteNo))
            return null;

        var sortUtc = DateTime.MinValue;
        if (doc.TryGetValue("createdAtUtc", out var cu) && cu.IsString
            && DateTime.TryParse(cu.AsString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            sortUtc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();

        var createdDisplay = sortUtc == DateTime.MinValue
            ? "—"
            : sortUtc.ToLocalTime().ToString("dd-MMM-yyyy", CultureInfo.GetCultureInfo("en-IN"));

        return new CreditNoteSearchRow
        {
            CreditNoteNo = creditNoteNo,
            ReturnNo = ReadString(doc, "returnNo") ?? "",
            OriginalBillNo = ReadString(doc, "originalBillNo") ?? "",
            CustomerName = ReadString(doc, "customerName") ?? "",
            CustomerPhone = ReadString(doc, "customerPhone") ?? "",
            Amount = ReadDecimal(doc, "amount"),
            RemainingAmount = ReadDecimal(doc, "remainingAmount"),
            Status = ReadString(doc, "status") ?? "",
            CreatedAtDisplay = createdDisplay,
            SortUtc = sortUtc,
        };
    }

    private static CustomerCreditNoteRecord Map(BsonDocument d)
    {
        var amount = ReadDecimal(d, "amount");
        var remaining = ReadDecimal(d, "remainingAmount");
        var totalApplied = ReadDecimal(d, "totalApplied");
        if (totalApplied <= 0 && amount > 0)
            totalApplied = Math.Max(0, amount - remaining);

        return new CustomerCreditNoteRecord
        {
            CreditNoteNo = ReadString(d, "creditNoteNo") ?? "",
            ReturnNo = ReadString(d, "returnNo") ?? "",
            OriginalBillNo = ReadString(d, "originalBillNo") ?? "",
            CustomerCode = ReadString(d, "customerCode") ?? "",
            CustomerPhone = ReadString(d, "customerPhone") ?? "",
            CustomerName = ReadString(d, "customerName") ?? "",
            Amount = amount,
            RemainingAmount = remaining,
            TotalApplied = totalApplied,
            Status = ReadString(d, "status") ?? "",
            ConsumedBillNo = ReadString(d, "consumedBillNo"),
            LastAppliedBillNo = ReadString(d, "lastAppliedBillNo"),
        };
    }

    private static string? ReadString(BsonDocument d, string key) =>
        d.TryGetValue(key, out var v) && !v.IsBsonNull
            ? v.IsString ? v.AsString : v.ToString()
            : null;

    private static decimal ReadDecimal(BsonDocument d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v.IsBsonNull)
            return 0m;
        return v switch
        {
            { IsDouble: true } => (decimal)v.AsDouble,
            { IsInt32: true } => v.AsInt32,
            { IsInt64: true } => v.AsInt64,
            { IsDecimal128: true } => (decimal)v.AsDecimal128,
            _ => 0m,
        };
    }
}
