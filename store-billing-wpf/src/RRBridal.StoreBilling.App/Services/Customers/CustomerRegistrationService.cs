using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services;

namespace RRBridal.StoreBilling.App.Services.Customers;

public sealed class CustomerRegistrationService
{
    private readonly IMongoDatabase _localDb;
    private readonly HttpClient _centralApi;
    private readonly StoreContext _storeContext;

    private static readonly JsonSerializerOptions JsonCamel = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public CustomerRegistrationService(IMongoDatabase localDb, HttpClient centralApi, StoreContext storeContext)
    {
        _localDb = localDb;
        _centralApi = centralApi;
        _storeContext = storeContext;
    }

    public async Task<CustomerRegistrationResult> RegisterAsync(CustomerRegistrationPayload p, CancellationToken ct = default)
    {
        var storeId = _storeContext.StoreId;
        var coll = _localDb.GetCollection<BsonDocument>("store_customers");

        var phoneCombined = CombinePhone(p.Telephone, p.Mobile);
        var (addressLine1, addressLine2) = BuildCentralAddress(p);

        var localDoc = new BsonDocument
        {
            { "storeId", storeId },
            { "customerCode", p.CustomerCode },
            { "name", p.CustomerName.Trim() },
            { "telephone", p.Telephone.Trim() },
            { "mobile", p.Mobile.Trim() },
            { "phone", phoneCombined },
            { "email", p.Email.Trim() },
            { "gstin", p.Gstin.Trim() },
            { "doorNo", p.DoorNo.Trim() },
            { "street", p.Street.Trim() },
            { "fullAddress", p.FullAddress.Trim() },
            { "place", p.Place.Trim() },
            { "city", p.City.Trim() },
            { "pincode", p.Pincode.Trim() },
            { "state", p.State.Trim() },
            { "landmark", p.Landmark.Trim() },
            { "isCreditCustomer", p.IsCreditCustomer },
            { "createdAtUtc", DateTime.UtcNow.ToString("O") },
            { "centralCustomerId", BsonNull.Value },
            { "centralSyncStatus", "pending" },
            { "lastCentralError", BsonNull.Value },
        };

        await coll.InsertOneAsync(localDoc, cancellationToken: ct);
        var localId = localDoc["_id"].ToString()!;

        string? centralId = null;
        var syncStatus = "pending";
        string? syncWarning = null;
        var billingCustomerCode = p.CustomerCode;

        var body = new CentralCreateCustomerBody
        {
            CustomerCode = string.IsNullOrWhiteSpace(p.CustomerCode) ? null : p.CustomerCode,
            Name = p.CustomerName.Trim(),
            Phone = string.IsNullOrWhiteSpace(phoneCombined) ? null : phoneCombined,
            Email = string.IsNullOrWhiteSpace(p.Email) ? null : p.Email.Trim(),
            Gstin = string.IsNullOrWhiteSpace(p.Gstin) ? null : p.Gstin.Trim(),
            AddressLine1 = string.IsNullOrWhiteSpace(addressLine1) ? null : addressLine1,
            AddressLine2 = string.IsNullOrWhiteSpace(addressLine2) ? null : addressLine2,
            City = string.IsNullOrWhiteSpace(p.City) ? null : p.City.Trim(),
            State = string.IsNullOrWhiteSpace(p.State) ? null : p.State.Trim(),
            Pincode = string.IsNullOrWhiteSpace(p.Pincode) ? null : p.Pincode.Trim(),
            IsActive = true,
        };

        try
        {
            using var response = await _centralApi.PostAsJsonAsync("/api/customers", body, JsonCamel, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                centralId = TryReadCentralId(raw);
                var centralCode = TryReadCustomerCode(raw);
                syncStatus = "synced";
                var update = Builders<BsonDocument>.Update
                    .Set("centralCustomerId", centralId ?? "")
                    .Set("centralSyncStatus", "synced")
                    .Unset("lastCentralError");
                if (!string.IsNullOrWhiteSpace(centralCode))
                {
                    update = update.Set("customerCode", centralCode);
                    billingCustomerCode = centralCode;
                }
                await coll.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", localDoc["_id"]), update, cancellationToken: ct);
            }
            else
            {
                syncStatus = "failed";
                var err = $"HTTP {(int)response.StatusCode}: {Truncate(raw, 500)}";
                syncWarning = "Saved locally. Central sync failed: " + err;
                await coll.UpdateOneAsync(
                    Builders<BsonDocument>.Filter.Eq("_id", localDoc["_id"]),
                    Builders<BsonDocument>.Update
                        .Set("centralSyncStatus", "failed")
                        .Set("lastCentralError", err),
                    cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            syncStatus = "failed";
            syncWarning = "Saved locally. Central sync failed: " + ex.Message;
            await coll.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", localDoc["_id"]),
                Builders<BsonDocument>.Update
                    .Set("centralSyncStatus", "failed")
                    .Set("lastCentralError", ex.Message),
                cancellationToken: ct);
        }

        return new CustomerRegistrationResult
        {
            LocalMongoId = localId,
            CentralCustomerId = centralId,
            CentralSyncStatus = syncStatus,
            CentralSyncWarning = syncWarning,
            CustomerName = p.CustomerName.Trim(),
            CustomerPhone = phoneCombined,
            DoorNo = p.DoorNo.Trim(),
            Street = p.Street.Trim(),
            FullAddress = p.FullAddress.Trim(),
            BillingCustomerCode = billingCustomerCode,
        };
    }

    private sealed class CentralCreateCustomerBody
    {
        public string? CustomerCode { get; init; }
        public required string Name { get; init; }
        public string? Phone { get; init; }
        public string? Email { get; init; }
        public string? Gstin { get; init; }
        public string? AddressLine1 { get; init; }
        public string? AddressLine2 { get; init; }
        public string? City { get; init; }
        public string? State { get; init; }
        public string? Pincode { get; init; }
        public bool IsActive { get; init; }
    }

    private static string CombinePhone(string tel, string mobile)
    {
        var t = tel.Trim();
        var m = mobile.Trim();
        if (t.Length == 0) return m;
        if (m.Length == 0) return t;
        return $"{t} / {m}";
    }

    private static (string line1, string line2) BuildCentralAddress(CustomerRegistrationPayload p)
    {
        var door = p.DoorNo.Trim();
        var street = p.Street.Trim();
        var full = p.FullAddress.Trim();
        var line1Parts = new[] { door, street, full }.Where(s => s.Length > 0);
        var line1 = string.Join(", ", line1Parts);

        var place = p.Place.Trim();
        var lm = p.Landmark.Trim();
        var line2Parts = new[] { place, lm }.Where(s => s.Length > 0);
        var line2 = string.Join(" · ", line2Parts);

        return (line1, line2);
    }

    private static string? TryReadCustomerCode(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("customerCode", out var codeEl) && codeEl.ValueKind == JsonValueKind.String)
                return codeEl.GetString();
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static string? TryReadCentralId(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("_id", out var idEl))
            {
                if (idEl.ValueKind == JsonValueKind.String)
                    return idEl.GetString();
                if (idEl.ValueKind == JsonValueKind.Object && idEl.TryGetProperty("$oid", out var oid))
                    return oid.GetString();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s[..max] + "…";
    }
}

public sealed class CustomerRegistrationPayload
{
    public string CustomerCode { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string Telephone { get; init; } = "";
    public string Mobile { get; init; } = "";
    public string Email { get; init; } = "";
    public string Gstin { get; init; } = "";
    public string DoorNo { get; init; } = "";
    public string Street { get; init; } = "";
    public string FullAddress { get; init; } = "";
    public string Place { get; init; } = "";
    public string City { get; init; } = "";
    public string Pincode { get; init; } = "";
    public string State { get; init; } = "";
    public string Landmark { get; init; } = "";
    public bool IsCreditCustomer { get; init; }
}
