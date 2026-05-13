using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.App.Services.Customers;

public sealed class CustomerMatch
{
    public string Source { get; init; } = "";
    public string Id { get; init; } = "";
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Email { get; init; } = "";
    public string DoorNo { get; init; } = "";
    public string Street { get; init; } = "";
    public string FullAddress { get; init; } = "";
    public string City { get; init; } = "";
    public string State { get; init; } = "";
    public string Pincode { get; init; } = "";
    public string Gstin { get; init; } = "";

    public string DisplayLine => string.IsNullOrWhiteSpace(Phone)
        ? Name
        : $"{Name}  —  {Phone}";
}

public sealed class CustomerLookupService
{
    private readonly IMongoDatabase _localDb;
    private readonly HttpClient _centralApi;

    public CustomerLookupService(IMongoDatabase localDb, HttpClient centralApi)
    {
        _localDb = localDb;
        _centralApi = centralApi;
    }

    public async Task<List<CustomerMatch>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var q = query.Trim();
        var results = new List<CustomerMatch>();

        var localResults = await SearchLocalAsync(q, ct);
        results.AddRange(localResults);

        var centralResults = await SearchCentralAsync(q, ct);
        foreach (var c in centralResults)
        {
            if (!results.Any(r => r.Id == c.Id))
                results.Add(c);
        }

        return results;
    }

    private async Task<List<CustomerMatch>> SearchLocalAsync(string query, CancellationToken ct)
    {
        var coll = _localDb.GetCollection<BsonDocument>("store_customers");
        var regex = new BsonRegularExpression(query, "i");
        var filter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Regex("name", regex),
            Builders<BsonDocument>.Filter.Regex("phone", regex),
            Builders<BsonDocument>.Filter.Regex("mobile", regex),
            Builders<BsonDocument>.Filter.Regex("telephone", regex),
            Builders<BsonDocument>.Filter.Regex("email", regex),
            Builders<BsonDocument>.Filter.Regex("customerCode", regex)
        );

        var docs = await coll.Find(filter).Limit(50).ToListAsync(ct);
        return docs.Select(d => new CustomerMatch
        {
            Source = "Local",
            Id = d.GetValue("centralCustomerId", BsonNull.Value).IsBsonNull
                ? d["_id"].ToString()!
                : d["centralCustomerId"].AsString,
            Code = d.GetValue("customerCode", "").AsString,
            Name = d.GetValue("name", "").AsString,
            Phone = d.GetValue("phone", "").AsString,
            Email = d.GetValue("email", "").AsString,
            DoorNo = d.GetValue("doorNo", "").AsString,
            Street = d.GetValue("street", "").AsString,
            FullAddress = d.GetValue("fullAddress", "").AsString,
            City = d.GetValue("city", "").AsString,
            State = d.GetValue("state", "").AsString,
            Pincode = d.GetValue("pincode", "").AsString,
            Gstin = d.GetValue("gstin", "").AsString,
        }).ToList();
    }

    private async Task<List<CustomerMatch>> SearchCentralAsync(string query, CancellationToken ct)
    {
        try
        {
            var url = $"/customers?search={Uri.EscapeDataString(query)}";
            var response = await _centralApi.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return [];

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var items = root.ValueKind == JsonValueKind.Array ? root : default;
            if (items.ValueKind != JsonValueKind.Array)
                return [];

            var results = new List<CustomerMatch>();
            foreach (var el in items.EnumerateArray())
            {
                results.Add(new CustomerMatch
                {
                    Source = "Central",
                    Id = el.TryGetProperty("_id", out var idEl) ? idEl.GetString() ?? "" : "",
                    Code = el.TryGetProperty("customerCode", out var cc) ? cc.GetString() ?? "" : "",
                    Name = el.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                    Phone = el.TryGetProperty("phone", out var p) ? p.GetString() ?? "" : "",
                    Email = el.TryGetProperty("email", out var e) ? e.GetString() ?? "" : "",
                    DoorNo = "",
                    Street = "",
                    FullAddress = el.TryGetProperty("addressLine1", out var a1) ? a1.GetString() ?? "" : "",
                    City = el.TryGetProperty("city", out var c) ? c.GetString() ?? "" : "",
                    State = el.TryGetProperty("state", out var s) ? s.GetString() ?? "" : "",
                    Pincode = el.TryGetProperty("pincode", out var pc) ? pc.GetString() ?? "" : "",
                    Gstin = el.TryGetProperty("gstin", out var g) ? g.GetString() ?? "" : "",
                });
            }

            return results;
        }
        catch
        {
            return [];
        }
    }
}
