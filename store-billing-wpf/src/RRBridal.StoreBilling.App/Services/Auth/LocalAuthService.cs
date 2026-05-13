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

    public LocalAuthService(IMongoDatabase localDb)
    {
        _storeUsers = localDb.GetCollection<BsonDocument>("store_users");
    }

    public async Task<IReadOnlyList<StoreUserRecord>> GetAllUsersAsync(CancellationToken ct = default)
    {
        var docs = await _storeUsers
            .Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(new BsonDocument("name", 1))
            .ToListAsync(ct);

        return docs.Select(MapToRecord).ToList();
    }

    public async Task<StoreUserRecord?> ValidateAsync(string email, string password, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var filter = Builders<BsonDocument>.Filter.Eq("email", normalizedEmail);
        var doc = await _storeUsers.Find(filter).FirstOrDefaultAsync(ct);
        if (doc is null) return null;

        var hash = doc.GetValue("passwordHash", "").AsString;
        if (string.IsNullOrEmpty(hash)) return null;

        if (!BCrypt.Net.BCrypt.Verify(password, hash))
            return null;

        return MapToRecord(doc);
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
        };
    }
}
