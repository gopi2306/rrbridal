using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.App.Services;

/// <summary>Creates indexes used by multi-counter reporting on shared MongoDB.</summary>
public static class StoreIndexEnsurer
{
    public static async Task EnsureAsync(IMongoDatabase db, CancellationToken ct = default)
    {
        var bills = db.GetCollection<MongoDB.Bson.BsonDocument>("store_bills");
        var keys = Builders<MongoDB.Bson.BsonDocument>.IndexKeys
            .Ascending("storeId")
            .Ascending("deviceId")
            .Descending("createdAtUtc");
        await bills.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(keys, new CreateIndexOptions { Name = "storeId_deviceId_createdAtUtc" }),
            cancellationToken: ct);
    }
}
