using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.App.Services.Auth;

public sealed class LocalAuthService
{
    private readonly IMongoCollection<BsonDocument> _storeUsers;
    private readonly StoreContext _storeContext;

    public LocalAuthService(IMongoDatabase localDb, StoreContext storeContext)
    {
        _storeUsers = localDb.GetCollection<BsonDocument>("store_users");
        _storeContext = storeContext;
    }

    public async Task<StoreUserRecord?> TryGetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var doc = await _storeUsers
            .Find(Builders<BsonDocument>.Filter.Eq("email", normalizedEmail))
            .FirstOrDefaultAsync(ct);
        return doc is null ? null : MapToRecord(doc);
    }

    public async Task<IReadOnlyList<StoreUserRecord>> GetAllUsersAsync(CancellationToken ct = default)
    {
        var docs = await _storeUsers
            .Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(new BsonDocument("name", 1))
            .ToListAsync(ct);

        return docs.Select(MapToRecord).ToList();
    }

    /// <summary>Login with machine lock: one active session per user per DEVICE_ID across store tills.</summary>
    public async Task<(StoreUserRecord? User, string? Error)> TryLoginAsync(
        string email,
        string password,
        CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var filter = Builders<BsonDocument>.Filter.Eq("email", normalizedEmail);
        var doc = await _storeUsers.Find(filter).FirstOrDefaultAsync(ct);
        if (doc is null)
            return (null, null);

        var hash = doc.GetValue("passwordHash", "").AsString;
        if (string.IsNullOrEmpty(hash) || !BCrypt.Net.BCrypt.Verify(password, hash))
            return (null, null);

        var activeMachineId = ReadString(doc, "activeMachineId");
        var currentDevice = _storeContext.DeviceId.Trim();
        if (!string.IsNullOrEmpty(activeMachineId)
            && !string.Equals(activeMachineId, currentDevice, StringComparison.OrdinalIgnoreCase))
        {
            var pos = ReadInt(doc, "activePosCounter");
            var tillLabel = pos > 0 ? $"POS{pos}" : "another till";
            return (null,
                $"This user is already logged in on {tillLabel} · {activeMachineId}. Log out on that till first.");
        }

        var posCounter = int.TryParse(_storeContext.PosCounter, out var pc) ? pc : 1;
        var update = Builders<BsonDocument>.Update
            .Set("activeMachineId", currentDevice)
            .Set("activePosCounter", posCounter);

        await _storeUsers.UpdateOneAsync(filter, update, cancellationToken: ct);

        doc["activeMachineId"] = currentDevice;
        doc["activePosCounter"] = posCounter;

        return (MapToRecord(doc), null);
    }

    /// <summary>Clears machine lock for this till only (logout / app exit).</summary>
    public async Task<bool> ReleaseSessionAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("email", normalizedEmail),
            Builders<BsonDocument>.Filter.Eq("activeMachineId", _storeContext.DeviceId));

        var update = Builders<BsonDocument>.Update
            .Unset("activeMachineId")
            .Unset("activePosCounter");

        var result = await _storeUsers.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount > 0 || result.MatchedCount > 0;
    }

    private static string? ReadString(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var v) || v.IsBsonNull)
            return null;
        var s = v.IsString ? v.AsString : v.ToString();
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    private static int ReadInt(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var v) || v.IsBsonNull)
            return 0;
        return v.IsInt32 ? v.AsInt32 : v.IsInt64 ? (int)v.AsInt64 : 0;
    }

    private static StoreUserRecord MapToRecord(BsonDocument doc)
    {
        return new StoreUserRecord
        {
            CentralId = doc.GetValue("centralId", "").AsString,
            Name = doc.GetValue("name", "").AsString,
            Email = doc.GetValue("email", "").AsString,
            Role = doc.GetValue("role", "").AsString,
            StoreId = doc.GetValue("storeId", "").AsString,
            MaxDiscountPercent = ReadMaxDiscountPercent(doc),
        };
    }

    private static decimal ReadMaxDiscountPercent(BsonDocument doc)
    {
        if (!doc.TryGetValue("maxDiscountPercent", out var v) || v.IsBsonNull)
            return 100m;

        decimal pct = v.IsDecimal128 ? (decimal)v.AsDecimal128
            : v.IsDouble ? (decimal)v.AsDouble
            : v.IsInt32 ? v.AsInt32
            : v.IsInt64 ? v.AsInt64
            : 100m;
        if (pct < 0) return 0;
        if (pct > 100) return 100;
        return pct;
    }
}
