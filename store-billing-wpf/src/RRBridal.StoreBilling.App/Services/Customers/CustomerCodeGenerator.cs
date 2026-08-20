using System;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.App.Services.Customers;

public sealed class CustomerCodeGenerator
{
    private const string CounterCollectionName = "counters";
    private const string CounterKey = "customerCode";
    private const string Prefix = "CUST";

    private readonly IMongoDatabase _localDb;
    private readonly Func<bool>? _useCentralCodeGeneration;

    public CustomerCodeGenerator(IMongoDatabase localDb, Func<bool>? useCentralCodeGeneration = null)
    {
        _localDb = localDb;
        _useCentralCodeGeneration = useCentralCodeGeneration;
    }

    public async Task<string> NextAsync(CancellationToken ct = default)
    {
        // In direct-online mode Central owns customer-code allocation. Returning an
        // empty code makes CustomerRegistrationService omit it from the request.
        if (_useCentralCodeGeneration?.Invoke() == true)
            return "";

        var counters = _localDb.GetCollection<BsonDocument>(CounterCollectionName);
        var filter = Builders<BsonDocument>.Filter.Eq("_id", CounterKey);
        var update = Builders<BsonDocument>.Update.Inc("seq", 1);
        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After,
        };

        var doc = await counters.FindOneAndUpdateAsync(filter, update, options, ct);
        var seq = doc["seq"].AsInt32;
        return $"{Prefix}-{seq:D4}";
    }
}
