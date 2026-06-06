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

        await bills.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("billNo"),
                new CreateIndexOptions { Name = "billNo" }),
            cancellationToken: ct);

        await bills.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Descending("createdAtUtc"),
                new CreateIndexOptions { Name = "createdAtUtc_desc" }),
            cancellationToken: ct);

        var creditNotes = db.GetCollection<MongoDB.Bson.BsonDocument>("customer_credit_notes");
        await creditNotes.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Ascending("customerPhoneNorm")
                    .Ascending("status"),
                new CreateIndexOptions { Name = "storeId_phone_status" }),
            cancellationToken: ct);
        await creditNotes.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("creditNoteNo"),
                new CreateIndexOptions { Name = "creditNoteNo", Unique = true }),
            cancellationToken: ct);

        var holds = db.GetCollection<MongoDB.Bson.BsonDocument>("held_bills");
        await holds.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Ascending("holdNo"),
                new CreateIndexOptions { Name = "storeId_holdNo", Unique = true }),
            cancellationToken: ct);
        await holds.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Descending("updatedAtUtc"),
                new CreateIndexOptions { Name = "storeId_updatedAtUtc" }),
            cancellationToken: ct);
    }
}
