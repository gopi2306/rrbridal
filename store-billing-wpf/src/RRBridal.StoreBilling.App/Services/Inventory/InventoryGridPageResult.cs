using System.Collections.Generic;

namespace RRBridal.StoreBilling.App.Services.Inventory;

/// <summary>
/// Paged store inventory grid (local Mongo), aligned with central <c>GET /api/inventory/grid</c> shape.
/// </summary>
public sealed class InventoryGridPageResult
{
    public required IReadOnlyList<InventoryGridRow> Data { get; init; }

    public int Total { get; init; }

    public int Page { get; init; }

    public int Limit { get; init; }

    public int TotalPages { get; init; }
}
