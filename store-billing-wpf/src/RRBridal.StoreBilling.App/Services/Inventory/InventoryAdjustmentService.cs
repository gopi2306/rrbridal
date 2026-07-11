using System;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Products;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Inventory;

public enum InventoryAdjustmentMode
{
    Add,
    Remove,
    SetTo,
}

public sealed class InventoryAdjustmentService
{
    private readonly IMongoCollection<BsonDocument> _localAdjustments;
    private readonly ProductCatalogService _productCatalog;
    private readonly BillingOutboxPublisher _outbox;
    private readonly StoreContext _storeContext;

    public InventoryAdjustmentService(
        IMongoDatabase localDb,
        ProductCatalogService productCatalog,
        BillingOutboxPublisher outbox,
        StoreContext storeContext)
    {
        _localAdjustments = localDb.GetCollection<BsonDocument>("local_inventory_adjustments");
        _productCatalog = productCatalog;
        _outbox = outbox;
        _storeContext = storeContext;
    }

    public async Task<(bool Success, string Message)> AdjustAsync(
        string sku,
        decimal currentQty,
        InventoryAdjustmentMode mode,
        decimal quantity,
        string reason,
        CancellationToken ct = default)
    {
        var trimmedSku = sku?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(trimmedSku))
            return (false, "SKU is required.");

        var trimmedReason = reason?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(trimmedReason))
            return (false, "Reason is required.");

        if (quantity < 0)
            return (false, "Quantity must be zero or greater.");

        if (mode is InventoryAdjustmentMode.Add or InventoryAdjustmentMode.Remove && quantity <= 0)
            return (false, "Quantity must be greater than zero.");

        var qtyDelta = mode switch
        {
            InventoryAdjustmentMode.Add => quantity,
            InventoryAdjustmentMode.Remove => -quantity,
            InventoryAdjustmentMode.SetTo => quantity - currentQty,
            _ => 0m,
        };

        if (qtyDelta == 0)
            return (false, "No change in quantity.");

        var qtyAfter = currentQty + qtyDelta;
        if (qtyAfter < 0)
            return (false, $"Resulting quantity cannot be negative (current {currentQty:N2}, change {qtyDelta:N2}).");

        if (qtyDelta > 0)
        {
            await _productCatalog.IncrementStockBySkuAsync(trimmedSku, qtyDelta, trimmedReason, ct);
        }
        else
        {
            await _productCatalog.DecrementStockBySkuAsync(
                trimmedSku,
                -qtyDelta,
                reason: "inventory_adjustment",
                actorName: trimmedReason,
                ct: ct);
        }

        var adjustmentNo = await AllocateLocalAdjustmentNoAsync(ct);
        var eventId = await _outbox.PublishInventoryAdjustmentCreatedAsync(
            adjustmentNo,
            trimmedReason,
            trimmedSku,
            qtyDelta,
            trimmedReason,
            ct);

        await RecordLocalAdjustmentAsync(eventId, adjustmentNo, trimmedSku, qtyDelta, trimmedReason, ct);
        return (true, "Stock adjusted locally and queued for sync.");
    }

    public async Task<bool> HasAppliedAsync(string? sourceEventId, string? centralAdjustmentId, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(sourceEventId))
        {
            var byEvent = await _localAdjustments
                .Find(Builders<BsonDocument>.Filter.Eq("sourceEventId", sourceEventId.Trim()))
                .Limit(1)
                .AnyAsync(ct);
            if (byEvent) return true;
        }

        if (!string.IsNullOrWhiteSpace(centralAdjustmentId))
        {
            return await _localAdjustments
                .Find(Builders<BsonDocument>.Filter.Eq("centralAdjustmentId", centralAdjustmentId.Trim()))
                .Limit(1)
                .AnyAsync(ct);
        }

        return false;
    }

    public async Task RecordPulledAdjustmentAsync(
        string? sourceEventId,
        string centralAdjustmentId,
        string adjustmentNo,
        string sku,
        decimal qtyDelta,
        string reason,
        CancellationToken ct = default)
    {
        var doc = new BsonDocument
        {
            { "sourceEventId", sourceEventId ?? "" },
            { "centralAdjustmentId", centralAdjustmentId },
            { "adjustmentNo", adjustmentNo },
            { "sku", sku },
            { "qtyDelta", (double)qtyDelta },
            { "reason", reason },
            { "appliedAtUtc", DateTime.UtcNow.ToString("O") },
            { "intakeSource", "central_pull" },
        };
        await _localAdjustments.InsertOneAsync(doc, cancellationToken: ct);
    }

    private async Task RecordLocalAdjustmentAsync(
        string eventId,
        string adjustmentNo,
        string sku,
        decimal qtyDelta,
        string reason,
        CancellationToken ct)
    {
        var doc = new BsonDocument
        {
            { "sourceEventId", eventId },
            { "adjustmentNo", adjustmentNo },
            { "sku", sku },
            { "qtyDelta", (double)qtyDelta },
            { "reason", reason },
            { "appliedAtUtc", DateTime.UtcNow.ToString("O") },
            { "intakeSource", "local_post" },
        };
        await _localAdjustments.InsertOneAsync(doc, cancellationToken: ct);
    }

    private async Task<string> AllocateLocalAdjustmentNoAsync(CancellationToken ct)
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"IADJ-{date}-";
        var filter = Builders<BsonDocument>.Filter.Regex("adjustmentNo", new BsonRegularExpression($"^{prefix}", "i"));
        var count = await _localAdjustments.CountDocumentsAsync(filter, cancellationToken: ct);
        return $"{prefix}{count + 1:D4}";
    }
}
