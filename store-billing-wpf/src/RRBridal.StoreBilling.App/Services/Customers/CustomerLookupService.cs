using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Customers;

public sealed class CustomerMatch
{
    public string Source { get; init; } = "";
    public string Id { get; init; } = "";
    /// <summary>Local Mongo <c>_id</c> when known (offline / hybrid). Empty for central-only rows.</summary>
    public string LocalMongoId { get; init; } = "";
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Email { get; init; } = "";
    public string DoorNo { get; init; } = "";
    public string Street { get; init; } = "";
    public string FullAddress { get; init; } = "";
    public string Place { get; init; } = "";
    public string City { get; init; } = "";
    public string State { get; init; } = "";
    public string Pincode { get; init; } = "";
    public string Gstin { get; init; } = "";
    public bool IsCreditCustomer { get; init; }

    public string DisplayLine => string.IsNullOrWhiteSpace(Phone)
        ? Name
        : $"{Name}  —  {Phone}";

    public string SuggestionLine
    {
        get
        {
            var baseLine = DisplayLine;
            var gstin = (Gstin ?? "").Trim();
            return string.IsNullOrEmpty(gstin) ? baseLine : $"{baseLine}  ·  GSTIN {gstin}";
        }
    }
}

public sealed class CustomerLookupService
{
    private readonly IMongoDatabase _localDb;
    private readonly HttpClient _centralApi;
    private readonly CentralOnlineModeService? _centralMode;

    public CustomerLookupService(IMongoDatabase localDb, HttpClient centralApi, CentralOnlineModeService? centralMode = null)
    {
        _localDb = localDb;
        _centralApi = centralApi;
        _centralMode = centralMode;
    }

    private bool IsCentralOnline => _centralMode?.IsOnlineMode == true;

    public async Task<List<CustomerMatch>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var q = query.Trim();

        if (IsCentralOnline)
        {
            var centralOnly = await SearchCentralAsync(q, ct);
            if (PhoneMatchHelper.IsPhoneLikeQuery(q))
            {
                var phoneMatches = centralOnly.Where(r => PhoneMatchHelper.PhoneMatches(r.Phone, q)).ToList();
                if (phoneMatches.Count > 0)
                    return phoneMatches;
            }
            return centralOnly;
        }

        var results = await SearchLocalAsync(q, ct);

        if (PhoneMatchHelper.IsPhoneLikeQuery(q))
        {
            var phoneMatches = results.Where(r => PhoneMatchHelper.PhoneMatches(r.Phone, q)).ToList();
            if (phoneMatches.Count > 0)
                return phoneMatches;
        }

        return results;
    }

    /// <summary>List recent customers from Central (no filter). Used by Customers page empty Search.</summary>
    public async Task<List<CustomerMatch>> ListCentralAsync(CancellationToken ct = default)
    {
        if (!IsCentralOnline)
            return [];
        return await FetchCentralAsync("/api/customers", ct);
    }

    /// <summary>Load one Central customer by id (full address / GST / email fields).</summary>
    public async Task<CustomerMatch?> GetCentralByIdAsync(string centralId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(centralId))
            return null;

        try
        {
            var response = await _centralApi.GetAsync($"/api/customers/{Uri.EscapeDataString(centralId.Trim())}", ct);
            if (!response.IsSuccessStatusCode)
            {
                var raw = await response.Content.ReadAsStringAsync(ct);
                if (IsCentralOnline)
                    throw new InvalidOperationException(
                        $"Central customer load failed: HTTP {(int)response.StatusCode}: {Truncate(raw, 300)}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            return MapCentralElement(doc.RootElement);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (IsCentralOnline)
                throw new InvalidOperationException("Central customer load failed: " + ex.Message, ex);

            Trace.TraceWarning("Central customer load error for '{0}': {1}", centralId, ex.Message);
            return null;
        }
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
        return docs.Select(d =>
        {
            var localId = d["_id"].ToString()!;
            var centralId = d.GetValue("centralCustomerId", BsonNull.Value).IsBsonNull
                ? ""
                : d["centralCustomerId"].AsString;
            return new CustomerMatch
            {
                Source = "Local",
                LocalMongoId = localId,
                Id = string.IsNullOrWhiteSpace(centralId) ? localId : centralId,
                Code = d.GetValue("customerCode", "").AsString,
                Name = d.GetValue("name", "").AsString,
                Phone = d.GetValue("phone", "").AsString,
                Email = d.GetValue("email", "").AsString,
                DoorNo = d.GetValue("doorNo", "").AsString,
                Street = d.GetValue("street", "").AsString,
                FullAddress = d.GetValue("fullAddress", "").AsString,
                Place = d.GetValue("place", "").AsString,
                City = d.GetValue("city", "").AsString,
                State = d.GetValue("state", "").AsString,
                Pincode = d.GetValue("pincode", "").AsString,
                Gstin = d.GetValue("gstin", "").AsString,
                IsCreditCustomer = d.Contains("isCreditCustomer") && d["isCreditCustomer"].ToBoolean(),
            };
        }).ToList();
    }

    private Task<List<CustomerMatch>> SearchCentralAsync(string query, CancellationToken ct) =>
        FetchCentralAsync($"/api/customers?search={Uri.EscapeDataString(query)}", ct);

    private async Task<List<CustomerMatch>> FetchCentralAsync(string url, CancellationToken ct)
    {
        try
        {
            var response = await _centralApi.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                var raw = await response.Content.ReadAsStringAsync(ct);
                if (IsCentralOnline)
                    throw new InvalidOperationException($"Central customer search failed: HTTP {(int)response.StatusCode}: {Truncate(raw, 300)}");

                Trace.TraceWarning(
                    "Central customer search failed: {StatusCode} {Reason} for '{Url}'",
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    url);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var items = root.ValueKind == JsonValueKind.Array ? root : default;
            if (items.ValueKind != JsonValueKind.Array)
                return [];

            var results = new List<CustomerMatch>();
            foreach (var el in items.EnumerateArray())
                results.Add(MapCentralElement(el));

            return results;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (IsCentralOnline)
                throw new InvalidOperationException("Central customer search failed: " + ex.Message, ex);

            Trace.TraceWarning("Central customer search error for '{0}': {1}", url, ex.Message);
            return [];
        }
    }

    private static CustomerMatch MapCentralElement(JsonElement el)
    {
        return new CustomerMatch
        {
            Source = "Central",
            Id = ReadJsonId(el),
            Code = ReadJsonString(el, "customerCode"),
            Name = ReadJsonString(el, "name"),
            Phone = ReadJsonString(el, "phone"),
            Email = ReadJsonString(el, "email"),
            DoorNo = "",
            Street = "",
            FullAddress = ReadJsonString(el, "addressLine1"),
            Place = ReadJsonString(el, "addressLine2"),
            City = ReadJsonString(el, "city"),
            State = ReadJsonString(el, "state"),
            Pincode = ReadJsonString(el, "pincode"),
            Gstin = ReadJsonString(el, "gstin"),
            IsCreditCustomer = el.TryGetProperty("isCreditCustomer", out var icc)
                && (icc.ValueKind == JsonValueKind.True
                    || (icc.ValueKind == JsonValueKind.String
                        && bool.TryParse(icc.GetString(), out var b) && b)),
        };
    }

    private static string ReadJsonString(JsonElement el, string propertyName) =>
        el.TryGetProperty(propertyName, out var p) ? p.GetString() ?? "" : "";

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";

    private static string ReadJsonId(JsonElement el)
    {
        if (!el.TryGetProperty("_id", out var idEl) && !el.TryGetProperty("id", out idEl))
            return "";

        if (idEl.ValueKind == JsonValueKind.String)
            return idEl.GetString() ?? "";

        if (idEl.ValueKind == JsonValueKind.Object && idEl.TryGetProperty("$oid", out var oid))
            return oid.GetString() ?? "";

        return idEl.ToString();
    }
}
