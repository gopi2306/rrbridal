using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.App.Services.Salesmen;

public sealed class SalesmanCodeGenerator
{
    private const string CounterCollectionName = "counters";
    private const string CounterKey = "salesmanCode";
    private const string Prefix = "SM";

    private readonly IMongoCollection<BsonDocument> _counters;

    public SalesmanCodeGenerator(IMongoDatabase localDb)
    {
        _counters = localDb.GetCollection<BsonDocument>(CounterCollectionName);
    }

    public async Task<string> NextAsync(CancellationToken ct = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", CounterKey);
        var update = Builders<BsonDocument>.Update.Inc("seq", 1);
        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After,
        };

        var doc = await _counters.FindOneAndUpdateAsync(filter, update, options, ct);
        var seq = doc["seq"].AsInt32;
        return $"{Prefix}{seq:D3}";
    }
}
