using System;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Auth;

namespace RRBridal.StoreBilling.App.Services.Audit;

public sealed class StoreAuditEvent
{
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public required string Action { get; init; }
    public string? Sku { get; init; }
    public BsonDocument? Metadata { get; init; }
    public string? ActorName { get; init; }
    public string? ActorEmail { get; init; }
}

/// <summary>Persists POS audit and product-change events to local MongoDB (<c>store_audit_logs</c>).</summary>
public sealed class StoreAuditLogService
{
    private readonly IMongoCollection<BsonDocument> _logs;
    private readonly StoreContext _storeContext;

    public StoreAuditLogService(IMongoDatabase localDb, StoreContext storeContext)
    {
        _logs = localDb.GetCollection<BsonDocument>("store_audit_logs");
        _storeContext = storeContext;
    }

    public static (string? Name, string? Email) ActorFromSession(UserSession? session)
    {
        var user = session?.LoggedInUser;
        if (user == null) return (null, null);
        return (user.Name, user.Email);
    }

    public async Task LogEventAsync(StoreAuditEvent evt, CancellationToken ct = default)
    {
        try
        {
            var doc = new BsonDocument
            {
                { "entityType", evt.EntityType.Trim() },
                { "entityId", evt.EntityId.Trim() },
                { "action", evt.Action.Trim() },
                { "storeId", _storeContext.StoreId },
                { "deviceId", _storeContext.DeviceId },
                { "posCounter", _storeContext.PosCounter },
                { "createdAtUtc", DateTime.UtcNow.ToString("O") },
            };

            if (!string.IsNullOrWhiteSpace(evt.Sku))
                doc["sku"] = evt.Sku.Trim();
            if (!string.IsNullOrWhiteSpace(evt.ActorName))
                doc["actorName"] = evt.ActorName.Trim();
            if (!string.IsNullOrWhiteSpace(evt.ActorEmail))
                doc["actorEmail"] = evt.ActorEmail.Trim();
            if (evt.Metadata != null && evt.Metadata.ElementCount > 0)
                doc["metadata"] = evt.Metadata;

            await _logs.InsertOneAsync(doc, cancellationToken: ct);
        }
        catch
        {
            /* audit must not break billing */
        }
    }

    public Task LogProductStockChangeAsync(
        string sku,
        string centralProductId,
        decimal qtyDelta,
        decimal stockBefore,
        decimal stockAfter,
        string reason,
        string? billNo = null,
        UserSession? session = null,
        CancellationToken ct = default)
    {
        var (name, email) = ActorFromSession(session);
        var meta = new BsonDocument
        {
            { "qtyDelta", (double)qtyDelta },
            { "stockBefore", (double)stockBefore },
            { "stockAfter", (double)stockAfter },
            { "reason", reason },
        };
        if (!string.IsNullOrWhiteSpace(billNo))
            meta["billNo"] = billNo.Trim();
        if (!string.IsNullOrWhiteSpace(centralProductId))
            meta["centralProductId"] = centralProductId.Trim();

        return LogEventAsync(new StoreAuditEvent
        {
            EntityType = "product",
            EntityId = string.IsNullOrWhiteSpace(centralProductId) ? sku.Trim() : centralProductId.Trim(),
            Action = qtyDelta < 0 ? "stock_decremented" : "stock_incremented",
            Sku = sku.Trim(),
            Metadata = meta,
            ActorName = name,
            ActorEmail = email,
        }, ct);
    }
}
