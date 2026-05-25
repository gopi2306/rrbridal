using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Customers;

namespace RRBridal.StoreBilling.App.Services.Billing;

public sealed class CustomerCreditNoteService
{
    public const string StatusAvailable = "available";
    public const string StatusConsumed = "consumed";

    private readonly IMongoCollection<BsonDocument> _notes;

    public CustomerCreditNoteService(IMongoDatabase localDb)
    {
        _notes = localDb.GetCollection<BsonDocument>("customer_credit_notes");
    }

    public async Task<string?> CreateFromReturnAsync(
        string returnNo,
        string originalBillNo,
        string customerCode,
        string customerName,
        string customerPhone,
        decimal creditBalance,
        string storeId,
        CancellationToken ct = default)
    {
        if (creditBalance <= 0 || string.IsNullOrWhiteSpace(returnNo))
            return null;

        var phoneNorm = PhoneMatchHelper.NormalizePhone(customerPhone);
        if (string.IsNullOrEmpty(phoneNorm) || phoneNorm.Length < 10)
            return null;

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

        await _notes.InsertOneAsync(doc, cancellationToken: ct);
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
        return result.ModifiedCount > 0;
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
