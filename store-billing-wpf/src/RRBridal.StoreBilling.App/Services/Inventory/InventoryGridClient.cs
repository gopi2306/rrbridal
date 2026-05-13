using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Inventory;

public sealed class InventoryGridClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public InventoryGridClient(HttpClient centralApi)
    {
        _http = centralApi;
    }

    public async Task<IReadOnlyList<InventoryGridRow>> SearchAsync(
        string search,
        string storeId,
        int limit = 100,
        CancellationToken ct = default)
    {
        var q = search?.Trim() ?? "";
        var sid = storeId?.Trim() ?? "";
        limit = Math.Clamp(limit, 1, 500);

        var url =
            $"/api/inventory/grid?search={Uri.EscapeDataString(q)}&storeId={Uri.EscapeDataString(sid)}&limit={limit}";
        var res = await _http.GetAsync(url, ct);
        res.EnsureSuccessStatusCode();

        var list = await res.Content.ReadFromJsonAsync<List<InventoryGridRow>>(JsonOptions, ct);
        return list ?? (IReadOnlyList<InventoryGridRow>)Array.Empty<InventoryGridRow>();
    }
}
