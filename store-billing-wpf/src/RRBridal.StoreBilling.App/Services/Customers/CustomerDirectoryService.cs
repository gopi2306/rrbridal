using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.App.Services.Customers;

public sealed class CustomerListRow
{
    public string? LocalMongoId { get; init; }
    public string Source { get; init; } = "Local";
    public string CustomerCode { get; init; } = "";
    public string Name { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Email { get; init; } = "";
    public bool IsCreditCustomer { get; init; }
    public string SyncStatus { get; init; } = "";
    public DateTime SortUtc { get; init; }
}

public sealed class CustomerDirectoryService
{
    private const string CollectionName = "store_customers";
    private readonly IMongoCollection<BsonDocument> _customers;
    private readonly CustomerLookupService _lookup;

    public CustomerDirectoryService(IMongoDatabase localDb, CustomerLookupService lookup)
    {
        _customers = localDb.GetCollection<BsonDocument>(CollectionName);
        _lookup = lookup;
    }

    public async Task<IReadOnlyList<CustomerListRow>> SearchAsync(
        string storeId,
        string? customerCode,
        string? customerName,
        string? customerPhone,
        int limit = 200,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var sid = (storeId ?? "").Trim();
        var docs = await _customers
            .Find(Builders<BsonDocument>.Filter.Eq("storeId", sid))
            .Sort(Builders<BsonDocument>.Sort.Descending("createdAtUtc"))
            .Limit(500)
            .ToListAsync(ct);

        IEnumerable<BsonDocument> query = docs;
        if (!string.IsNullOrWhiteSpace(customerCode))
        {
            var q = customerCode.Trim();
            query = query.Where(d =>
                (d.GetValue("customerCode", "").AsString ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            var n = customerName.Trim();
            query = query.Where(d =>
                (d.GetValue("name", "").AsString ?? "").Contains(n, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(customerPhone))
        {
            var digits = Regex.Replace(customerPhone, @"\D", "");
            query = query.Where(d =>
            {
                var phone = Regex.Replace(d.GetValue("phone", "").AsString ?? "", @"\D", "");
                var mobile = Regex.Replace(d.GetValue("mobile", "").AsString ?? "", @"\D", "");
                return phone.Contains(digits, StringComparison.Ordinal)
                    || mobile.Contains(digits, StringComparison.Ordinal);
            });
        }

        var localRows = query
            .Select(MapLocalRow)
            .Where(r => r != null)
            .Cast<CustomerListRow>()
            .Take(limit)
            .ToList();

        var combinedQuery = string.Join(" ",
            new[] { customerCode, customerName, customerPhone }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (string.IsNullOrWhiteSpace(combinedQuery))
            return localRows;

        var centralMatches = await _lookup.SearchAsync(combinedQuery, ct);
        var merged = new List<CustomerListRow>(localRows);
        foreach (var match in centralMatches)
        {
            if (match.Source != "Central")
                continue;
            if (merged.Any(r =>
                    (!string.IsNullOrWhiteSpace(r.CustomerCode) && r.CustomerCode == match.Code)
                    || (!string.IsNullOrWhiteSpace(r.Phone) && PhoneMatchHelper.PhoneMatches(r.Phone, match.Phone))))
                continue;

            merged.Add(new CustomerListRow
            {
                Source = "Central",
                CustomerCode = match.Code,
                Name = match.Name,
                Phone = match.Phone,
                Email = match.Email,
                SyncStatus = "central",
                SortUtc = DateTime.MinValue,
            });
        }

        return merged
            .OrderByDescending(r => r.SortUtc)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    public async Task<BsonDocument?> GetLocalByIdAsync(string localMongoId, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(localMongoId, out var oid))
            return null;
        return await _customers.Find(Builders<BsonDocument>.Filter.Eq("_id", oid)).FirstOrDefaultAsync(ct);
    }

    private static CustomerListRow? MapLocalRow(BsonDocument doc)
    {
        var name = doc.GetValue("name", "").AsString;
        if (string.IsNullOrWhiteSpace(name))
            return null;

        DateTime sortUtc = DateTime.MinValue;
        var created = doc.GetValue("createdAtUtc", "").AsString;
        if (!string.IsNullOrWhiteSpace(created)
            && DateTime.TryParse(created, out var dt))
            sortUtc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();

        return new CustomerListRow
        {
            LocalMongoId = doc["_id"].ToString(),
            Source = "Local",
            CustomerCode = doc.GetValue("customerCode", "").AsString,
            Name = name,
            Phone = doc.GetValue("phone", doc.GetValue("mobile", "").AsString).AsString,
            Email = doc.GetValue("email", "").AsString,
            IsCreditCustomer = doc.Contains("isCreditCustomer") && doc["isCreditCustomer"].ToBoolean(),
            SyncStatus = doc.GetValue("centralSyncStatus", "").AsString,
            SortUtc = sortUtc,
        };
    }
}
