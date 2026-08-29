using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Customers;

public sealed class CustomerRegistrationService
{
    private readonly IMongoDatabase _localDb;
    private readonly HttpClient _centralApi;
    private readonly StoreContext _storeContext;
    private readonly CentralOnlineModeService? _centralMode;
    private readonly CustomerLookupService _lookup;

    private static readonly JsonSerializerOptions JsonCamel = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public CustomerRegistrationService(
        IMongoDatabase localDb,
        HttpClient centralApi,
        StoreContext storeContext,
        CentralOnlineModeService? centralMode = null,
        CustomerLookupService? lookup = null)
    {
        _localDb = localDb;
        _centralApi = centralApi;
        _storeContext = storeContext;
        _centralMode = centralMode;
        _lookup = lookup ?? new CustomerLookupService(localDb, centralApi, centralMode);
    }

    private bool IsCentralOnline => _centralMode?.IsOnlineMode == true;
    private bool IsCentralOffline => _centralMode != null && !_centralMode.IsOnlineMode;

    public async Task<CustomerRegistrationResult> RegisterAsync(CustomerRegistrationPayload p, CancellationToken ct = default)
    {
        var storeId = _storeContext.StoreId;
        var phoneCombined = CombinePhone(p.Telephone, p.Mobile);
        var (addressLine1, addressLine2) = BuildCentralAddress(p);

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
            IsCreditCustomer = p.IsCreditCustomer,
        };

        if (IsCentralOnline)
        {
            using var response = await _centralApi.PostAsJsonAsync("/api/customers", body, JsonCamel, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Central customer create failed: HTTP {(int)response.StatusCode}: {Truncate(raw, 300)}");

            var centralId = TryReadCentralId(raw);
            var centralCode = TryReadCustomerCode(raw) ?? p.CustomerCode;
            return new CustomerRegistrationResult
            {
                LocalMongoId = "",
                CentralCustomerId = centralId,
                CentralSyncStatus = "synced",
                CentralSyncWarning = null,
                CustomerName = p.CustomerName.Trim(),
                CustomerPhone = phoneCombined,
                Gstin = p.Gstin.Trim(),
                DoorNo = p.DoorNo.Trim(),
                Street = p.Street.Trim(),
                FullAddress = p.FullAddress.Trim(),
                BillingCustomerCode = centralCode,
            };
        }

        var coll = _localDb.GetCollection<BsonDocument>("store_customers");
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

        string? centralIdOffline = null;
        var syncStatus = "pending";
        string? syncWarning = null;
        var billingCustomerCode = p.CustomerCode;

        if (IsCentralOffline)
            return new CustomerRegistrationResult
            {
                LocalMongoId = localId,
                CentralCustomerId = null,
                CentralSyncStatus = "pending",
                CentralSyncWarning = "Saved locally (Central mode is Offline).",
                CustomerName = p.CustomerName.Trim(),
                CustomerPhone = phoneCombined,
                Gstin = p.Gstin.Trim(),
                DoorNo = p.DoorNo.Trim(),
                Street = p.Street.Trim(),
                FullAddress = p.FullAddress.Trim(),
                BillingCustomerCode = billingCustomerCode,
            };

        try
        {
            using var response = await _centralApi.PostAsJsonAsync("/api/customers", body, JsonCamel, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                centralIdOffline = TryReadCentralId(raw);
                var centralCode = TryReadCustomerCode(raw);
                syncStatus = "synced";
                var update = Builders<BsonDocument>.Update
                    .Set("centralCustomerId", centralIdOffline ?? "")
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
            CentralCustomerId = centralIdOffline,
            CentralSyncStatus = syncStatus,
            CentralSyncWarning = syncWarning,
            CustomerName = p.CustomerName.Trim(),
            CustomerPhone = phoneCombined,
            Gstin = p.Gstin.Trim(),
            DoorNo = p.DoorNo.Trim(),
            Street = p.Street.Trim(),
            FullAddress = p.FullAddress.Trim(),
            BillingCustomerCode = billingCustomerCode,
        };
    }

    /// <summary>Persists GSTIN for an existing customer matched from billing search.</summary>
    public Task<CustomerRegistrationResult> UpdateGstinFromMatchAsync(
        CustomerMatch customer,
        string gstin,
        CancellationToken ct = default)
    {
        var payload = new CustomerRegistrationPayload
        {
            CustomerCode = customer.Code ?? "",
            CustomerName = customer.Name ?? "",
            Mobile = customer.Phone ?? "",
            Email = customer.Email ?? "",
            Gstin = (gstin ?? "").Trim().ToUpperInvariant(),
            DoorNo = customer.DoorNo ?? "",
            Street = customer.Street ?? "",
            FullAddress = customer.FullAddress ?? "",
            Place = customer.Place ?? "",
            City = customer.City ?? "",
            State = customer.State ?? "",
            Pincode = customer.Pincode ?? "",
            IsCreditCustomer = customer.IsCreditCustomer,
        };

        var localId = !string.IsNullOrWhiteSpace(customer.LocalMongoId)
            ? customer.LocalMongoId
            : (string.Equals(customer.Source, "Local", StringComparison.OrdinalIgnoreCase) ? customer.Id : "");
        var centralId = !string.IsNullOrWhiteSpace(customer.Id)
            && (string.Equals(customer.Source, "Central", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(customer.LocalMongoId)
                    && !string.Equals(customer.Id, customer.LocalMongoId, StringComparison.Ordinal)))
            ? customer.Id
            : null;

        return UpdateAsync(localId, payload, centralId, ct);
    }

    public async Task<CustomerRegistrationResult> UpdateAsync(
        string localMongoId,
        CustomerRegistrationPayload p,
        string? centralCustomerId = null,
        CancellationToken ct = default)
    {
        if (IsCentralOnline)
            return await UpdateCentralOnlyAsync(localMongoId, centralCustomerId, p, ct);

        if (!ObjectId.TryParse(localMongoId, out var oid))
            throw new InvalidOperationException("Invalid customer id.");

        var coll = _localDb.GetCollection<BsonDocument>("store_customers");
        var existing = await coll.Find(Builders<BsonDocument>.Filter.Eq("_id", oid)).FirstOrDefaultAsync(ct);
        if (existing == null)
            throw new InvalidOperationException("Customer not found.");

        var phoneCombined = CombinePhone(p.Telephone, p.Mobile);
        var (addressLine1, addressLine2) = BuildCentralAddress(p);
        var now = DateTime.UtcNow.ToString("O");

        var update = Builders<BsonDocument>.Update
            .Set("name", p.CustomerName.Trim())
            .Set("telephone", p.Telephone.Trim())
            .Set("mobile", p.Mobile.Trim())
            .Set("phone", phoneCombined)
            .Set("email", p.Email.Trim())
            .Set("gstin", p.Gstin.Trim())
            .Set("doorNo", p.DoorNo.Trim())
            .Set("street", p.Street.Trim())
            .Set("fullAddress", p.FullAddress.Trim())
            .Set("place", p.Place.Trim())
            .Set("city", p.City.Trim())
            .Set("pincode", p.Pincode.Trim())
            .Set("state", p.State.Trim())
            .Set("landmark", p.Landmark.Trim())
            .Set("isCreditCustomer", p.IsCreditCustomer)
            .Set("updatedAtUtc", now);

        await coll.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", oid), update, cancellationToken: ct);

        var customerCode = existing.GetValue("customerCode", p.CustomerCode).AsString;
        var centralId = existing.GetValue("centralCustomerId", BsonNull.Value);
        var syncStatus = existing.GetValue("centralSyncStatus", "pending").AsString;
        string? syncWarning = null;

        if (IsCentralOffline)
        {
            syncWarning = "Saved locally (Central mode is Offline).";
        }
        else if (!centralId.IsBsonNull && !string.IsNullOrWhiteSpace(centralId.AsString))
        {
            var body = new CentralUpdateCustomerBody
            {
                Name = p.CustomerName.Trim(),
                Phone = string.IsNullOrWhiteSpace(phoneCombined) ? null : phoneCombined,
                Email = string.IsNullOrWhiteSpace(p.Email) ? null : p.Email.Trim(),
                Gstin = string.IsNullOrWhiteSpace(p.Gstin) ? null : p.Gstin.Trim(),
                AddressLine1 = string.IsNullOrWhiteSpace(addressLine1) ? null : addressLine1,
                AddressLine2 = string.IsNullOrWhiteSpace(addressLine2) ? null : addressLine2,
                City = string.IsNullOrWhiteSpace(p.City) ? null : p.City.Trim(),
                State = string.IsNullOrWhiteSpace(p.State) ? null : p.State.Trim(),
                Pincode = string.IsNullOrWhiteSpace(p.Pincode) ? null : p.Pincode.Trim(),
                IsCreditCustomer = p.IsCreditCustomer,
            };

            try
            {
                using var response = await _centralApi.PatchAsJsonAsync(
                    $"/api/customers/{centralId.AsString}",
                    body,
                    JsonCamel,
                    ct);
                if (response.IsSuccessStatusCode)
                {
                    syncStatus = "synced";
                    await coll.UpdateOneAsync(
                        Builders<BsonDocument>.Filter.Eq("_id", oid),
                        Builders<BsonDocument>.Update
                            .Set("centralSyncStatus", "synced")
                            .Unset("lastCentralError"),
                        cancellationToken: ct);
                }
                else
                {
                    var raw = await response.Content.ReadAsStringAsync(ct);
                    if (IsCentralOnline)
                        throw new InvalidOperationException($"Central customer update failed: HTTP {(int)response.StatusCode}: {Truncate(raw, 300)}");

                    syncStatus = "failed";
                    syncWarning = $"Saved locally. Central sync failed: HTTP {(int)response.StatusCode}";
                    await coll.UpdateOneAsync(
                        Builders<BsonDocument>.Filter.Eq("_id", oid),
                        Builders<BsonDocument>.Update
                            .Set("centralSyncStatus", "failed")
                            .Set("lastCentralError", Truncate(raw, 500)),
                        cancellationToken: ct);
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (IsCentralOnline)
                    throw new InvalidOperationException("Central customer update failed: " + ex.Message, ex);

                syncStatus = "failed";
                syncWarning = "Saved locally. Central sync failed: " + ex.Message;
                await coll.UpdateOneAsync(
                    Builders<BsonDocument>.Filter.Eq("_id", oid),
                    Builders<BsonDocument>.Update
                        .Set("centralSyncStatus", "failed")
                        .Set("lastCentralError", ex.Message),
                    cancellationToken: ct);
            }
        }
        else if (IsCentralOnline)
        {
            throw new InvalidOperationException("Customer is not linked to central. Cannot update in Online mode.");
        }

        return new CustomerRegistrationResult
        {
            LocalMongoId = localMongoId,
            CentralCustomerId = centralId.IsBsonNull ? null : centralId.AsString,
            CentralSyncStatus = syncStatus,
            CentralSyncWarning = syncWarning,
            CustomerName = p.CustomerName.Trim(),
            CustomerPhone = phoneCombined,
            Gstin = p.Gstin.Trim(),
            DoorNo = p.DoorNo.Trim(),
            Street = p.Street.Trim(),
            FullAddress = p.FullAddress.Trim(),
            BillingCustomerCode = customerCode,
        };
    }

    private async Task<CustomerRegistrationResult> UpdateCentralOnlyAsync(
        string localMongoId,
        string? centralCustomerId,
        CustomerRegistrationPayload p,
        CancellationToken ct)
    {
        var centralId = !string.IsNullOrWhiteSpace(centralCustomerId)
            ? centralCustomerId.Trim()
            : (!ObjectId.TryParse(localMongoId, out _) && !string.IsNullOrWhiteSpace(localMongoId) ? localMongoId.Trim() : null);

        if (string.IsNullOrWhiteSpace(centralId))
            throw new InvalidOperationException("Customer is not linked to central. Cannot update in Online mode.");

        var phoneCombined = CombinePhone(p.Telephone, p.Mobile);
        var (addressLine1, addressLine2) = BuildCentralAddress(p);

        var body = new CentralUpdateCustomerBody
        {
            Name = p.CustomerName.Trim(),
            Phone = string.IsNullOrWhiteSpace(phoneCombined) ? null : phoneCombined,
            Email = string.IsNullOrWhiteSpace(p.Email) ? null : p.Email.Trim(),
            Gstin = string.IsNullOrWhiteSpace(p.Gstin) ? null : p.Gstin.Trim(),
            AddressLine1 = string.IsNullOrWhiteSpace(addressLine1) ? null : addressLine1,
            AddressLine2 = string.IsNullOrWhiteSpace(addressLine2) ? null : addressLine2,
            City = string.IsNullOrWhiteSpace(p.City) ? null : p.City.Trim(),
            State = string.IsNullOrWhiteSpace(p.State) ? null : p.State.Trim(),
            Pincode = string.IsNullOrWhiteSpace(p.Pincode) ? null : p.Pincode.Trim(),
            IsCreditCustomer = p.IsCreditCustomer,
        };

        using var response = await _centralApi.PatchAsJsonAsync($"/api/customers/{centralId}", body, JsonCamel, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central customer update failed: HTTP {(int)response.StatusCode}: {Truncate(raw, 300)}");

        var updatedCode = TryReadCustomerCode(raw) ?? p.CustomerCode;

        return new CustomerRegistrationResult
        {
            LocalMongoId = "",
            CentralCustomerId = centralId,
            CentralSyncStatus = "synced",
            CentralSyncWarning = null,
            CustomerName = p.CustomerName.Trim(),
            CustomerPhone = phoneCombined,
            Gstin = p.Gstin.Trim(),
            DoorNo = p.DoorNo.Trim(),
            Street = p.Street.Trim(),
            FullAddress = p.FullAddress.Trim(),
            BillingCustomerCode = updatedCode,
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
        public bool IsCreditCustomer { get; init; }
    }

    private sealed class CentralUpdateCustomerBody
    {
        public required string Name { get; init; }
        public string? Phone { get; init; }
        public string? Email { get; init; }
        public string? Gstin { get; init; }
        public string? AddressLine1 { get; init; }
        public string? AddressLine2 { get; init; }
        public string? City { get; init; }
        public string? State { get; init; }
        public string? Pincode { get; init; }
        public bool IsCreditCustomer { get; init; }
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
            if (root.TryGetProperty("_id", out var idEl) || root.TryGetProperty("id", out idEl))
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

    public async Task<bool> IsCreditCustomerAsync(string? customerCode, string? customerPhone, CancellationToken ct = default)
    {
        var code = (customerCode ?? "").Trim();
        var phone = (customerPhone ?? "").Trim();

        if (IsCentralOnline)
        {
            var query = !string.IsNullOrEmpty(code) ? code : phone;
            if (string.IsNullOrEmpty(query))
                return false;

            var matches = await _lookup.SearchAsync(query, ct);
            var match = matches.FirstOrDefault(m =>
                (!string.IsNullOrEmpty(code) && string.Equals(m.Code, code, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(phone) && PhoneMatchHelper.PhoneMatches(m.Phone, phone)));
            return match?.IsCreditCustomer ?? false;
        }

        var coll = _localDb.GetCollection<BsonDocument>("store_customers");
        var filters = new List<FilterDefinition<BsonDocument>>();
        if (!string.IsNullOrEmpty(code))
            filters.Add(Builders<BsonDocument>.Filter.Eq("customerCode", code));
        if (!string.IsNullOrEmpty(phone))
        {
            filters.Add(Builders<BsonDocument>.Filter.Eq("phone", phone));
            filters.Add(Builders<BsonDocument>.Filter.Eq("mobile", phone));
        }

        if (filters.Count == 0)
            return false;

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", _storeContext.StoreId),
            Builders<BsonDocument>.Filter.Or(filters));

        var doc = await coll.Find(filter).FirstOrDefaultAsync(ct);
        if (doc == null)
            return false;
        return doc.Contains("isCreditCustomer") && doc["isCreditCustomer"].ToBoolean();
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
