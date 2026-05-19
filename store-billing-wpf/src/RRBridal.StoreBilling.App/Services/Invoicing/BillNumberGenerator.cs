using System;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public sealed class BillNumberGenerator
{
    private const string CounterCollectionName = "counters";

    private readonly IMongoCollection<BsonDocument> _counters;
    private readonly StoreContext _store;

    public BillNumberGenerator(IMongoDatabase localDb, StoreContext store)
    {
        _counters = localDb.GetCollection<BsonDocument>(CounterCollectionName);
        _store = store;
    }

    public async Task<string> NextAsync(CancellationToken ct = default)
    {
        var counterKey = $"billNo:{_store.StoreId}:{_store.DeviceId}";
        var filter = Builders<BsonDocument>.Filter.Eq("_id", counterKey);
        var update = Builders<BsonDocument>.Update.Inc("seq", 1);
        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After,
        };

        var doc = await _counters.FindOneAndUpdateAsync(filter, update, options, ct);
        var seq = doc["seq"].AsInt32;
        var date = DateTime.Now.ToString("yyyyMMdd");
        var pos = _store.PosCounter;
        return $"{date}-{pos}-{seq:D4}";
    }
}
